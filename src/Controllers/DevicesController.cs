using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XamantaSDK.Auth;
using XamantaSDK.Controllers.Models;
using XamantaSDK.Data.Entities;
using XamantaSDK.Data.Stores;
using XamantaSDK.Infrastructure;
using XamantaSDK.Services;

namespace XamantaSDK.Controllers;

[ApiController]
[Route("devices")]
[Tags("Devices")]
[Authorize(AuthenticationSchemes = AdminApiKeyDefaults.AuthenticationScheme)]
public class DevicesController(
    DeviceStore devices,
    PolicyStore policies,
    ClientStore clients,
    DeviceConnectionRegistry registry,
    PolicyDispatcher dispatcher,
    DeviceEraser eraser,
    ILogger<DevicesController> logger) : ControllerBase
{
    /// <summary>Lists every device that has ever synced.</summary>
    /// <remarks>
    /// A row appears the first time a device opens a policy stream, not when it registers, and it
    /// goes away only when the device is deleted. Takes no parameters: the list is neither paged
    /// nor filtered.
    /// </remarks>
    /// <response code="200">All known devices, in no guaranteed order.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<DeviceView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var items = await devices.ListAsync();
        var registrations = (await clients.GetByIdsAsync(items.Select(d => d.DeviceId).ToList()))
            .ToDictionary(c => c.ClientId);
        return Ok(items.Select(d => ToView(d, registrations.GetValueOrDefault(d.DeviceId))));
    }

    /// <summary>Reads the status of one device.</summary>
    /// <param name="deviceId">Device identity — the OAuth <c>client_id</c>, which is the Android ID.</param>
    /// <response code="200">The device's current status.</response>
    /// <response code="404">No device has synced under that id.</response>
    [HttpGet("{deviceId}")]
    [ProducesResponseType<DeviceView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string deviceId)
    {
        var device = await devices.GetAsync(deviceId);
        return device is null
            ? UnknownDevice(deviceId)
            : Ok(ToView(device, await clients.FindByIdAsync(deviceId)));
    }

    /// <summary>Lists the compliance reports a device has sent, newest first.</summary>
    /// <remarks>
    /// One report per apply run. The device reports <c>PENDING_COMPLIANCE</c> once a policy has
    /// been written to its storage, <c>IN_FORCE</c> once fully applied and <c>DEFIANCE</c> on
    /// terminal failure, so a single policy version usually leaves more than one report behind.
    /// A policy superseded before the device acted on it is never applied and never reported.
    /// </remarks>
    /// <param name="deviceId">Device identity — the OAuth <c>client_id</c>, which is the Android ID.</param>
    /// <param name="limit">Maximum number of reports to return. Defaults to 50.</param>
    /// <response code="200">The requested slice of the device's compliance history.</response>
    /// <response code="404">No device has synced under that id.</response>
    [HttpGet("{deviceId}/compliance")]
    [ProducesResponseType<IEnumerable<ComplianceReportView>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ComplianceHistory(string deviceId, [FromQuery] int limit = 50)
    {
        if (await devices.GetAsync(deviceId) is null)
            return UnknownDevice(deviceId);

        var reports = await devices.ListReportsAsync(deviceId, limit);
        return Ok(reports.Select(r => new ComplianceReportView(
            r.ExecutionId,
            ((Compliance)r.Compliance).ToString(),
            r.PolicyName,
            r.PolicyVersion,
            r.ReceivedAt,
            ProtoJson.Raw(r.Data))));
    }

    /// <summary>Assigns a published policy to a device.</summary>
    /// <remarks>
    /// The assignment is persisted whether or not the device is reachable. A connected device is
    /// pushed the policy's latest version immediately; a disconnected one receives it on its next
    /// sync. Assigning replaces any previous assignment, and there is no way to go back to the
    /// implicit <c>default</c> fallback through this endpoint.
    /// </remarks>
    /// <param name="deviceId">Device identity — the OAuth <c>client_id</c>, which is the Android ID.</param>
    /// <param name="request">The policy to assign.</param>
    /// <response code="204">Assigned, and pushed if the device was connected.</response>
    /// <response code="400"><c>policyName</c> was missing or blank.</response>
    /// <response code="404">The policy has never been published, or no device has synced under that id.</response>
    [HttpPost("{deviceId}/policy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignPolicy(string deviceId, [FromBody] AssignPolicyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PolicyName))
            return BadRequest(new ApiError("policyName is required"));

        var policy = await policies.GetLatestAsync(request.PolicyName);
        if (policy is null)
        {
            logger.LogWarning("Policy assignment rejected: policy {PolicyName} does not exist", request.PolicyName);
            return NotFound(new ApiError($"Policy '{request.PolicyName}' does not exist"));
        }

        if (!await devices.AssignPolicyAsync(deviceId, request.PolicyName))
        {
            logger.LogWarning("Policy assignment failed: device {DeviceId} is not known", deviceId);
            return UnknownDevice(deviceId);
        }

        logger.LogInformation("Assigned policy {PolicyName} to device {DeviceId}", request.PolicyName, deviceId);

        await dispatcher.TryPushAsync(deviceId, policy, "policy-assignment");
        return NoContent();
    }

    /// <summary>Deletes a device and everything the server holds about it.</summary>
    /// <remarks>
    /// Removes the status row, the whole compliance history and the OAuth registration with every
    /// token issued to it. The authentication attempt log is kept, since it is an audit trail
    /// rather than device state.
    ///
    /// This is a server-side removal, not a remote wipe: nothing is sent to the device, and a
    /// terminal still in the field simply stops being served. Its open policy stream and command
    /// channel are closed at once, and its access token stops working immediately even though it
    /// has not expired. Use it as the manual fallback when a <c>WIPE</c> succeeded but its result
    /// never reached the server, so the automatic removal never ran.
    ///
    /// The Android ID is free to register again afterwards, but the device needs a fresh
    /// enrollment token: the activation it spent on the original enrollment is not given back.
    ///
    /// It also deletes a device that registered but never synced, which <c>GET /devices/{deviceId}</c>
    /// reports as unknown. A 404 here therefore means nothing at all is held under that id.
    /// </remarks>
    /// <param name="deviceId">Device identity — the OAuth <c>client_id</c>, which is the Android ID.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Nothing is held under that id.</response>
    [HttpDelete("{deviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string deviceId)
    {
        return await eraser.EraseAsync(deviceId, "admin request")
            ? NoContent()
            : UnknownDevice(deviceId);
    }

    private IActionResult UnknownDevice(string deviceId) =>
        NotFound(new ApiError($"Device '{deviceId}' is not known"));

    private DeviceView ToView(Device d, Client? registration) => new(
        d.DeviceId,
        registry.IsConnected(d.DeviceId),
        d.PolicyName ?? PolicyStore.DefaultName,
        d.FirstSeenAt,
        d.LastSyncAt,
        d.LastContactAt,
        d.LastSentPolicyName,
        d.LastSentPolicyVersion,
        d.AppliedPolicyName,
        d.AppliedPolicyVersion,
        d.LastComplianceStatus is int c ? ((Compliance)c).ToString() : null,
        d.LastComplianceAt,
        ProtoJson.Raw(d.SyncInfoJson),
        ToRegistrationView(registration));

    private static DeviceRegistrationView? ToRegistrationView(Client? c) => c is null ? null : new(
        c.RegisteredAt,
        c.EnrollmentId,
        ProtoJson.Raw(c.MetadataJson));
}
