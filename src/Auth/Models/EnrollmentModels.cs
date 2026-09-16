using System.Text.Json.Serialization;

namespace XamantaSDK.Auth.Models;

public class CreateEnrollmentRequest
{
    /// <summary>
    /// Space-delimited scope tokens this enrollment may ever grant. It is the ceiling for the
    /// devices it registers: both their registration and their later token requests are narrowed
    /// against it. Omit for no scope.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>How many devices may register with this one token. Defaults to 1; must be at least 1.</summary>
    [JsonPropertyName("max_activations")]
    public int? MaxActivations { get; set; }

    /// <summary>
    /// Seconds until the token expires, counted from now. Defaults to 3600; must be at least 1.
    /// There is no value meaning "never" — every enrollment expires.
    /// </summary>
    [JsonPropertyName("expires_in_seconds")]
    public int? ExpiresInSeconds { get; set; }
}

/// <param name="EnrollmentId">Identifier used to list or revoke this enrollment.</param>
/// <param name="EnrollmentToken">The plaintext token, shown here and nowhere else. Not recoverable once lost.</param>
/// <param name="MaxActivations">How many devices may still register in total with this token.</param>
/// <param name="ExpiresAt">When the token stops working (UTC).</param>
public record CreateEnrollmentResponse(
    [property: JsonPropertyName("enrollment_id")] string EnrollmentId,
    [property: JsonPropertyName("enrollment_token")] string EnrollmentToken,
    [property: JsonPropertyName("max_activations")] int MaxActivations,
    [property: JsonPropertyName("expires_at")] DateTime ExpiresAt
);

/// <summary>An enrollment as listed. Never carries the token.</summary>
/// <param name="EnrollmentId">Identifier used to revoke it.</param>
/// <param name="Scope">Scope ceiling for the devices it registers. Null for no scope.</param>
/// <param name="MaxActivations">How many registrations the token allows in total.</param>
/// <param name="ActivationCount">How many have been spent.</param>
/// <param name="ExpiresAt">When it expires (UTC).</param>
/// <param name="RevokedAt">When it was revoked (UTC). Null when it has not been.</param>
/// <param name="CreatedAt">When it was minted (UTC).</param>
/// <param name="Active">Whether a device could still register with it: not revoked, not expired, activations left.</param>
public record EnrollmentView(
    [property: JsonPropertyName("enrollment_id")] string EnrollmentId,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("max_activations")] int MaxActivations,
    [property: JsonPropertyName("activation_count")] int ActivationCount,
    [property: JsonPropertyName("expires_at")] DateTime ExpiresAt,
    [property: JsonPropertyName("revoked_at")] DateTime? RevokedAt,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("active")] bool Active
);
