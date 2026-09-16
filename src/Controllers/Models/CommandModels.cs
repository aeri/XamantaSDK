namespace XamantaSDK.Controllers.Models;

/// <summary>
/// Returned when the device accepted the command but did not report a result within the timeout.
/// The command was delivered and its outcome is still unknown: it may have run, or the device may
/// have let it expire. Correlate the eventual result by <paramref name="CommandId"/> rather than
/// reissuing blindly — RESET_PASSWORD and REMARRY are not safe to repeat.
/// </summary>
/// <param name="Error">Human-readable reason.</param>
/// <param name="CommandId">Server-generated id of the command that was sent.</param>
public record CommandTimeout(string Error, string CommandId);
