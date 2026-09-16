using System.Text.Json;
using System.Text.Json.Serialization;

namespace XamantaSDK.Auth.Models;

public class ClientRegistrationRequest
{
    /// <summary>
    /// Refused if non-empty: a <c>client_credentials</c> client never visits an authorization
    /// endpoint. Answered with <c>invalid_redirect_uri</c>.
    /// </summary>
    [JsonPropertyName("redirect_uris")]
    public string[]? RedirectUris { get; set; }

    /// <summary>
    /// How the client will authenticate at the token endpoint: <c>client_secret_basic</c> (the §2
    /// default, used when omitted) or <c>client_secret_post</c>. Anything else is refused.
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; set; }

    /// <summary>
    /// Refused unless it is exactly <c>["client_credentials"]</c>. Omitting it registers that value —
    /// not the §2 default of <c>["authorization_code"]</c>, which this server cannot grant.
    /// </summary>
    [JsonPropertyName("grant_types")]
    public string[]? GrantTypes { get; set; }

    /// <summary>Refused if non-empty, and registered as <c>[]</c>.</summary>
    [JsonPropertyName("response_types")]
    public string[]? ResponseTypes { get; set; }

    /// <summary>Human-readable device name, at most 128 characters. Descriptive only.</summary>
    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }

    /// <summary>
    /// Space-delimited scope tokens, which must be a subset of the enrollment's. Omit to be granted
    /// the enrollment's whole set.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// Refused: this server issues shared-secret clients only. Must not be sent together with
    /// <c>jwks</c>.
    /// </summary>
    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; set; }

    /// <summary>
    /// Refused: this server issues shared-secret clients only. Must not be sent together with
    /// <c>jwks_uri</c>.
    /// </summary>
    [JsonPropertyName("jwks")]
    public JsonElement? Jwks { get; set; }

    /// <summary>Application id of the DPC build. Descriptive only.</summary>
    [JsonPropertyName("software_id")]
    public string? SoftwareId { get; set; }

    /// <summary>Version of the DPC build. Descriptive only.</summary>
    [JsonPropertyName("software_version")]
    public string? SoftwareVersion { get; set; }

    /// <summary>
    /// Refused: this server trusts no statement issuer, so it cannot honour one. Answered with
    /// <c>invalid_software_statement</c>.
    /// </summary>
    [JsonPropertyName("software_statement")]
    public string? SoftwareStatement { get; set; }

    /// <summary>
    /// Required, at most 128 characters. Stable device identity, and the <c>client_id</c> the server
    /// issues. Registering it twice is refused.
    /// </summary>
    [JsonPropertyName("android_id")]
    public string? AndroidId { get; set; }

    /// <summary>
    /// Best-effort hardware descriptor. Absent on a device with no cellular radio. Never used to
    /// identify the device.
    /// </summary>
    [JsonPropertyName("imei")]
    public string? Imei { get; set; }

    /// <summary>
    /// Best-effort hardware descriptor. Absent on a device with no cellular radio. Never used to
    /// identify the device.
    /// </summary>
    [JsonPropertyName("meid")]
    public string? Meid { get; set; }

    /// <summary>Best-effort hardware descriptor. Never used to identify the device.</summary>
    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    /// <summary>
    /// Best-effort hardware descriptor, present only once an organization id has been set. Never used
    /// to identify the device.
    /// </summary>
    [JsonPropertyName("esid")]
    public string? Esid { get; set; }

    /// <summary>Best-effort hardware descriptor: <c>Build.MANUFACTURER</c>.</summary>
    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    /// <summary>Best-effort hardware descriptor: <c>Build.BRAND</c>.</summary>
    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    /// <summary>Best-effort hardware descriptor: <c>Build.MODEL</c>.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

/// <summary>The metadata the server registered, after its own defaults and rejections.</summary>
public sealed record ClientMetadata(
    string ClientId,
    string? Scope,
    Dictionary<string, object> Document);

/// <summary>RFC 7591 §3.2.1 client information response.</summary>
/// <param name="ClientId">The issued <c>client_id</c>, always equal to the <c>android_id</c> that was sent.</param>
/// <param name="ClientSecret">The issued secret, in the clear and only here. Used for <c>client_secret_basic</c> or <c>client_secret_post</c>.</param>
/// <param name="ClientIdIssuedAt">When the <c>client_id</c> was issued, as seconds since the Unix epoch.</param>
/// <param name="ClientSecretExpiresAt">Always <c>0</c> — the issued secret does not expire.</param>
public record ClientRegistrationResponse(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_secret")] string ClientSecret,
    [property: JsonPropertyName("client_id_issued_at")] long ClientIdIssuedAt,
    [property: JsonPropertyName("client_secret_expires_at")] long ClientSecretExpiresAt)
{
    /// <summary>
    /// Every registered metadata value, spliced in as top-level members of the response object
    /// (§3.2.1): the ones the device sent and were accepted, plus <c>token_endpoint_auth_method</c>,
    /// <c>grant_types</c>, <c>response_types</c> and <c>scope</c> as the server settled them.
    /// Values with nothing to report are omitted.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> Metadata { get; init; } = [];
}
