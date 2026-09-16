namespace XamantaSDK.Infrastructure;

/// <summary>Error body of the admin REST API.</summary>
/// <param name="Error">Human-readable reason. There is no machine-readable code — the status carries the meaning.</param>
public record ApiError(string Error);
