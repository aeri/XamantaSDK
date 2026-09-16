using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XamantaSDK.Auth;
using XamantaSDK.Controllers.Models;
using XamantaSDK.Infrastructure;
using XamantaSDK.Services;

namespace XamantaSDK.Controllers;

[ApiController]
[Route("devices")]
[Tags("Devices")]
[Authorize(AuthenticationSchemes = AdminApiKeyDefaults.AuthenticationScheme)]
public class CommandController(DeviceConnectionRegistry registry, ILogger<CommandController> logger) : ControllerBase
{
    /// <summary>Issues a one-shot command to a device and waits for its result.</summary>
    /// <remarks>
    /// Synchronous: the request stays open until the device answers or the timeout elapses. The
    /// command travels over the device's open <c>commands.CommandService</c> channel, so the
    /// device must be connected right now — commands are not queued for later delivery.
    ///
    /// The server fills in <c>id</c>, <c>issuedAt</c> and <c>durationSeconds</c>; sending them is
    /// pointless, they are overwritten. Which member of the <c>params</c> group applies is decided by <c>type</c>:
    /// <c>RESET_PASSWORD</c> reads <c>resetPasswordParams</c>, <c>WIPE</c> reads <c>wipeParams</c>,
    /// <c>CLEAR_APP_DATA</c> reads <c>clearAppDataParams</c> and <c>REMARRY</c> reads
    /// <c>remarryParams</c>; the rest take none.
    ///
    /// The DPC does not deduplicate. <c>LOCK</c>, <c>REBOOT</c>, <c>WIPE</c>, <c>CLEAR_APP_DATA</c>
    /// and <c>DECOMMISSION</c> are safe to reissue; <c>RESET_PASSWORD</c> and <c>REMARRY</c> are not.
    /// <c>REBOOT</c>, <c>WIPE</c> and <c>REMARRY</c> report success just before acting, because the
    /// action itself tears the connection down.
    ///
    /// A successful <c>WIPE</c> or <c>DECOMMISSION</c> ends the enrolment, so the server erases the
    /// device the moment the result arrives, exactly as <c>DELETE /devices/{deviceId}</c> would.
    /// The erase runs after this response is produced, so the device can still appear in
    /// <c>GET /devices</c> for a moment afterwards.
    /// </remarks>
    /// <param name="deviceId">Device identity — the OAuth <c>client_id</c>, which is the Android ID.</param>
    /// <param name="command">The <c>commands.Command</c> to run, in proto3 JSON.</param>
    /// <param name="timeoutSeconds">
    /// How long the command is valid, in seconds. Defaults to 100. It is both how long this request
    /// waits for a result and the <c>durationSeconds</c> the device is told, so a command the admin
    /// has given up on is not executed late: the device answers <c>FAILED</c> with
    /// <c>EXPIRED</c> instead.
    /// </param>
    /// <response code="200">The device reported a result. A <c>FAILED</c> status is still a 200 — read <c>status</c>.</response>
    /// <response code="404">The device holds no open command channel on this server.</response>
    /// <response code="503">The command channel was closed while the command was being written to it.</response>
    /// <response code="504">The command was delivered but no result arrived within <paramref name="timeoutSeconds"/>.</response>
    [HttpPost("{deviceId}/command")]
    [ProducesResponseType<CommandResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<CommandTimeout>(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> IssueCommand(
        string deviceId,
        [FromBody] Command command,
        [FromQuery] int timeoutSeconds = 100)
    {
        if (!registry.Commands.TryGetWriter(deviceId, out var writer))
        {
            logger.LogWarning("Command {Type} rejected: device {DeviceId} is not connected to command channel",
                command.Type, deviceId);
            return NotFound(new ApiError($"Device '{deviceId}' is not connected to command channel"));
        }

        command.Id = Guid.NewGuid().ToString();
        command.IssuedAt = DateTimeOffset.UtcNow.ToString("o");
        command.DurationSeconds = timeoutSeconds;

        var tcs = registry.TrackCommand(command.Id, command.Type);

        if (!writer.TryWrite(command))
        {
            logger.LogWarning("Command {CommandId} ({Type}) dropped: channel closed for device {DeviceId}",
                command.Id, command.Type, deviceId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiError("Command channel is closed"));
        }

        logger.LogInformation("Issued command {CommandId} ({Type}) to device {DeviceId}",
            command.Id, command.Type, deviceId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        cts.Token.Register(() => tcs.TrySetCanceled());

        try
        {
            var result = await tcs.Task;
            logger.LogInformation("Command {CommandId} completed with status {Status} on device {DeviceId}",
                command.Id, result.Status, deviceId);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Command {CommandId} ({Type}) to device {DeviceId} timed out after {TimeoutSeconds}s",
                command.Id, command.Type, deviceId, timeoutSeconds);
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new CommandTimeout("Device did not respond in time", command.Id));
        }
    }
}
