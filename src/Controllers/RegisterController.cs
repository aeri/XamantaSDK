using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XamantaSDK.Auth;
using XamantaSDK.Auth.Models;
using XamantaSDK.Data.Entities;
using XamantaSDK.Data.Stores;
using XamantaSDK.Infrastructure;

namespace XamantaSDK.Controllers;

[ApiController]
[Route("connect")]
[Tags("OAuth")]
public class RegisterController(
    EnrollmentStore enrollmentStore,
    ClientStore clientStore,
    SecretProtector protector,
    ILogger<RegisterController> logger) : ControllerBase
{
    /// <summary>Registers a device and issues its long-lived OAuth credentials.</summary>
    /// <remarks>
    /// RFC 7591 dynamic client registration, called once during provisioning. The device presents
    /// its enrollment token as the §3.1 initial access token in <c>Authorization: Bearer</c> and
    /// sends its client metadata; the server answers with a <c>client_id</c> — always the
    /// <c>android_id</c> — and a <c>client_secret</c> that does not expire. Keying on
    /// <c>android_id</c> rather than the IMEI lets devices with no cellular radio enrol.
    ///
    /// Every metadata value is a top-level member of the request object (§3.1). The device must
    /// not send <c>client_id</c> or <c>client_secret</c>. Members the server does not understand
    /// — <c>client_uri</c>, <c>logo_uri</c>, <c>tos_uri</c>, <c>policy_uri</c>, <c>contacts</c>
    /// and anything else — are dropped silently and neither stored nor echoed, as §2 requires.
    ///
    /// This server issues shared-secret <c>client_credentials</c> clients and nothing else, so the
    /// metadata that would leave such a client inconsistent is refused outright instead of being
    /// quietly ignored: <c>redirect_uris</c>, a <c>grant_types</c> other than
    /// <c>["client_credentials"]</c>, a non-empty <c>response_types</c>, <c>jwks</c>,
    /// <c>jwks_uri</c> and <c>software_statement</c>. Omitted <c>grant_types</c> defaults to
    /// <c>["client_credentials"]</c> — not to the §2 default of <c>["authorization_code"]</c>,
    /// which this server could never honour.
    ///
    /// <c>scope</c> is narrowed against the enrollment's: omitting it grants the enrollment's full
    /// set, asking for a subset restricts it, and asking outside it fails. One <c>android_id</c>
    /// registers once — a second attempt is refused, whether the first has committed or is racing.
    /// </remarks>
    /// <param name="request">RFC 7591 §2 client metadata, plus this server's private extensions.</param>
    /// <response code="201">
    /// Registered. Carries the issued credentials and every registered metadata value, including
    /// the ones the server provisioned itself, with <c>Cache-Control: no-store</c>.
    /// </response>
    /// <response code="400">
    /// <c>invalid_client_metadata</c>, <c>invalid_redirect_uri</c> or
    /// <c>invalid_software_statement</c>. All permanent — retrying the same body will not help.
    /// </response>
    /// <response code="401">
    /// <c>invalid_token</c>: the enrollment token is missing, unknown, expired, revoked or already
    /// spent. Carries <c>WWW-Authenticate: Bearer</c>.
    /// </response>
    /// <response code="415">The body was not <c>application/json</c>.</response>
    [HttpPost("register")]
    [NoStore]
    [Consumes("application/json")]
    [OAuthErrors("invalid_client_metadata", "application/json")]
    [ProducesResponseType<ClientRegistrationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<TokenError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<TokenError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<TokenError>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> Register([FromBody] ClientRegistrationRequest request)
    {
        var enrollmentToken = Request.BearerToken();
        if (string.IsNullOrEmpty(enrollmentToken))
        {
            logger.LogWarning("Device registration rejected: missing enrollment token");
            return BearerChallenge(null, "Missing enrollment token");
        }

        var enrollment = await enrollmentStore.FindActiveByTokenAsync(enrollmentToken);
        if (enrollment is null)
        {
            logger.LogWarning("Device registration rejected: invalid, expired or exhausted enrollment token");
            return BearerChallenge("invalid_token", "Invalid, expired or exhausted enrollment token");
        }

        if (!ClientMetadataValidator.TryNormalize(request, enrollment.AllowedScopes, out var metadata, out var error))
        {
            logger.LogWarning("Device registration rejected for enrollment {EnrollmentId}: {Error} ({Description})",
                enrollment.Id, error.Error, error.ErrorDescription);
            return BadRequest(error);
        }

        if (await clientStore.FindByIdAsync(metadata.ClientId) is not null)
        {
            logger.LogWarning("Device registration rejected: android_id {AndroidId} already registered", metadata.ClientId);
            return BadRequest(new TokenError("invalid_client_metadata", "device already registered"));
        }

        var secret = protector.NewToken();
        var now = DateTime.UtcNow;
        var client = new Client
        {
            ClientId = metadata.ClientId,
            ClientSecret = protector.Derive(secret),
            AllowedScopes = metadata.Scope,
            MetadataJson = JsonSerializer.Serialize(metadata.Document),
            EnrollmentId = enrollment.Id,
            RegisteredAt = now
        };

        try
        {
            await clientStore.CreateDeviceAsync(client);
        }
        catch (DbUpdateException)
        {
            logger.LogWarning("Device registration rejected: android_id {AndroidId} already registered (lost race)", metadata.ClientId);
            return BadRequest(new TokenError("invalid_client_metadata", "device already registered"));
        }

        await enrollmentStore.IncrementActivationAsync(enrollment);

        logger.LogInformation("Registered device {ClientId} via enrollment {EnrollmentId}", metadata.ClientId, enrollment.Id);

        return StatusCode(StatusCodes.Status201Created, new ClientRegistrationResponse(
            metadata.ClientId,
            secret,
            new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeSeconds(),
            0)
        {
            Metadata = metadata.Document
        });
    }

    private IActionResult BearerChallenge(string? error, string description)
    {
        Response.Headers.WWWAuthenticate = error is null
            ? $"Bearer realm=\"{AuthorizationHeader.Realm}\""
            : $"Bearer realm=\"{AuthorizationHeader.Realm}\", error=\"{TokenError.Sanitize(error)}\", error_description=\"{TokenError.Sanitize(description)}\"";
        return Unauthorized(new TokenError(error ?? "invalid_token", description));
    }
}
