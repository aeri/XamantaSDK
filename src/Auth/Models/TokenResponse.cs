using System.Text.Json.Serialization;

namespace XamantaSDK.Auth.Models;

/// <summary>RFC 6749 §5.1 access token response.</summary>
/// <param name="AccessToken">Signed JWT. Presented as <c>Authorization: Bearer</c> on every gRPC call.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="ExpiresIn">Lifetime in seconds, fixed per enrollment (3600 unless set otherwise).</param>
/// <param name="Scope">The scope actually granted. Omitted when the client has no scope at all.</param>
public record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Scope
);

/// <summary>
/// Error body of the OAuth endpoints (RFC 6749 §5.2, RFC 7591 §3.2.2). Also used by
/// <c>POST /enrollments</c>, which shares their error shape.
/// </summary>
/// <param name="Error">The RFC error code the client is expected to act on.</param>
/// <param name="ErrorDescription">Human-readable detail. Never machine-parsed.</param>
public record TokenError(string Error, string? ErrorDescription)
{
    [JsonPropertyName("error")]
    public string Error { get; } = Sanitize(Error)!;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; } = Sanitize(ErrorDescription);

    public static string? Sanitize(string? value) =>
        value is null || value.All(IsAllowed) ? value : new string(value.Where(IsAllowed).ToArray());

    private static bool IsAllowed(char c) => c is >= ' ' and <= '~' and not '"' and not '\\';
}
