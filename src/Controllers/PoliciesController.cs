using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XamantaSDK.Auth;
using XamantaSDK.Controllers.Models;
using XamantaSDK.Data.Stores;
using XamantaSDK.Infrastructure;
using XamantaSDK.Services;

namespace XamantaSDK.Controllers;

[ApiController]
[Route("policies")]
[Tags("Policies")]
[Authorize(AuthenticationSchemes = AdminApiKeyDefaults.AuthenticationScheme)]
public class PoliciesController(
    PolicyStore policies,
    DeviceStore devices,
    PolicyDispatcher dispatcher,
    ILogger<PoliciesController> logger) : ControllerBase
{
    /// <summary>Publishes a new version of a named policy.</summary>
    /// <remarks>
    /// Despite the verb, the body replaces the policy whole: it is stored as submitted, not merged
    /// into the previous version, and each call creates a new immutable version numbered from 1.
    /// Publishing a name for the first time creates it — there is no separate create.
    ///
    /// The server overwrites <c>name</c> with the path segment and <c>version</c> with the number
    /// it assigns, so sending either is pointless.
    ///
    /// Every device whose effective policy is this name is pushed the new version at once if it is
    /// connected, and picks it up on its next sync otherwise. That includes devices with no
    /// explicit assignment when the name is <c>default</c>.
    /// </remarks>
    /// <param name="name">Policy name. <c>default</c> is the one unassigned devices fall back to.</param>
    /// <param name="policy">The <c>policy.Policy</c> to publish, in proto3 JSON.</param>
    /// <response code="201">Published. The body is the stored document, with <c>name</c> and the assigned <c>version</c> filled in.</response>
    [HttpPatch("{name}")]
    [ProducesResponseType<Policy>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Publish(string name, [FromBody] Policy policy)
    {
        var stored = await policies.CreateVersionAsync(name, policy);

        foreach (var deviceId in await devices.ListDeviceIdsWithEffectivePolicyAsync(name))
            await dispatcher.TryPushAsync(deviceId, stored, "policy-update");

        logger.LogInformation("Published policy {PolicyName} v{Version}", stored.Name, stored.Version);

        return StatusCode(StatusCodes.Status201Created, policy);
    }

    /// <summary>Lists every policy at its latest version.</summary>
    /// <remarks>Names only — the policy documents themselves are not included. Takes no parameters.</remarks>
    /// <response code="200">All published policies. Empty until something has been published.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<PolicySummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var items = await policies.ListLatestAsync();
        return Ok(items.Select(p => new PolicySummary(p.Name, p.Version, p.CreatedAt)));
    }

    /// <summary>Reads the latest version of a policy.</summary>
    /// <remarks>Only the latest: older versions are kept but cannot be read back through this API.</remarks>
    /// <param name="name">Policy name.</param>
    /// <response code="200">The stored <c>policy.Policy</c>, in proto3 JSON.</response>
    /// <response code="404">Nothing has ever been published under that name.</response>
    [HttpGet("{name}")]
    [ProducesResponseType<Policy>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatest(string name)
    {
        var policy = await policies.GetLatestAsync(name);
        if (policy is null)
            return NotFound(new ApiError($"Policy '{name}' does not exist"));

        return Ok(ProtoJson.Parser.Parse<Policy>(policy.Data));
    }

    /// <summary>Lists the published versions of a policy, newest first.</summary>
    /// <param name="name">Policy name.</param>
    /// <response code="200">Every version ever published under that name.</response>
    /// <response code="404">Nothing has ever been published under that name.</response>
    [HttpGet("{name}/versions")]
    [ProducesResponseType<IEnumerable<PolicyVersionView>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Versions(string name)
    {
        var items = await policies.ListVersionsAsync(name);
        if (items.Count == 0)
            return NotFound(new ApiError($"Policy '{name}' does not exist"));

        return Ok(items.Select(p => new PolicyVersionView(p.Version, p.CreatedAt)));
    }
}
