namespace XamantaSDK.Controllers.Models;

/// <summary>A named policy, at its latest version.</summary>
/// <param name="Name">Policy name. Devices with no explicit assignment fall back to <c>default</c>.</param>
/// <param name="Version">Highest published version number.</param>
/// <param name="CreatedAt">When that version was published (UTC).</param>
public record PolicySummary(string Name, int Version, DateTime CreatedAt);

/// <summary>One published version of a policy. Versions are immutable.</summary>
/// <param name="Version">Version number, auto-incremented from 1 on each publication.</param>
/// <param name="CreatedAt">When it was published (UTC).</param>
public record PolicyVersionView(int Version, DateTime CreatedAt);
