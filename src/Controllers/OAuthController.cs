using Microsoft.AspNetCore.Mvc;
using XamantaSDK.Auth;
using XamantaSDK.Auth.Models;
using XamantaSDK.Data.Stores;
using XamantaSDK.Infrastructure;

namespace XamantaSDK.Controllers;

[ApiController]
[Route("connect")]
[Tags("OAuth")]
public class OAuthController(
    TokenService tokenService,
    AuthAttemptStore authAttemptStore,
    ILogger<OAuthController> logger) : ControllerBase
{
    private static readonly string[] RecognizedParameters = ["grant_type", "client_id", "client_secret", "scope"];

    /// <summary>Exchanges a device's client credentials for an access token.</summary>
    /// <remarks>
    /// RFC 6749 §3.2 token endpoint. The only grant is <c>client_credentials</c>; the token it
    /// returns is the bearer credential the gRPC services accept, and it is not accepted by any
    /// operation in this document.
    ///
    /// The client authenticates either with <c>Authorization: Basic</c>
    /// (<c>client_secret_basic</c>, recommended) or with <c>client_id</c> and <c>client_secret</c>
    /// in the form body (<c>client_secret_post</c>) — never both, which is an
    /// <c>invalid_request</c> (§2.3.1). A body <c>client_id</c> that contradicts the authenticated
    /// one is refused rather than silently resolved; a bare <c>client_id</c> beside a Basic header
    /// is not authentication, so the header simply wins.
    ///
    /// None of the four parameters may be sent more than once (§3.2), and one sent without a value
    /// counts as omitted. Unrecognised parameters are ignored, repeated or not.
    ///
    /// - `grant_type` — required. Must be `client_credentials`.
    /// - `client_id` — the registered client id, which is the device's Android ID. Required for
    ///   `client_secret_post`; redundant beside a Basic header, and refused if it names a
    ///   different client.
    /// - `client_secret` — the secret issued at registration, for `client_secret_post` only.
    /// - `scope` — space-delimited, case-sensitive, and a subset of what the client was granted at
    ///   registration. Omit to be granted that whole set.
    /// </remarks>
    /// <param name="request">The form-encoded token request.</param>
    /// <response code="200">An access token, sent with <c>Cache-Control: no-store</c> and <c>Pragma: no-cache</c> (§5.1).</response>
    /// <response code="400">
    /// <c>invalid_request</c> (no <c>grant_type</c>, a duplicated parameter, two authentication
    /// mechanisms at once, a contradictory <c>client_id</c>, or an unreadable body),
    /// <c>unsupported_grant_type</c>, or <c>invalid_scope</c>.
    /// </response>
    /// <response code="401">
    /// <c>invalid_client</c>: credentials missing, unknown, wrong, expired, or an unsupported
    /// <c>Authorization</c> scheme. Always carries <c>WWW-Authenticate: Basic</c>.
    /// </response>
    /// <response code="415">The body was not <c>application/x-www-form-urlencoded</c>.</response>
    [HttpPost("token")]
    [NoStore]
    [Consumes("application/x-www-form-urlencoded")]
    [OAuthErrors("invalid_request", "application/x-www-form-urlencoded")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TokenError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<TokenError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<TokenError>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> Token([FromForm] TokenRequest request)
    {
        if (DuplicateParameter() is { } duplicate)
        {
            await LogAttemptAsync(request, false, $"duplicate parameter {duplicate}");
            logger.LogWarning("Token request rejected: parameter {Parameter} sent more than once", duplicate);
            return BadRequest(new TokenError("invalid_request", $"Parameter '{duplicate}' is included more than once"));
        }

        if (!TryResolveClientCredentials(request, out var credentialError))
        {
            await LogAttemptAsync(request, false, credentialError);
            logger.LogWarning("Token request rejected: {Reason}", credentialError);
            return BadRequest(new TokenError("invalid_request", credentialError));
        }

        if (string.IsNullOrEmpty(request.GrantType))
        {
            await LogAttemptAsync(request, false, "missing grant_type");
            logger.LogWarning("Token request from client {ClientId} rejected: grant_type is required", request.ClientId);
            return BadRequest(new TokenError("invalid_request", "grant_type is required"));
        }

        try
        {
            IActionResult result = request.GrantType switch
            {
                "client_credentials" => Ok(await tokenService.HandleClientCredentialsAsync(
                    request.ClientId ?? "", request.ClientSecret ?? "", request.Scope)),
                _ => BadRequest(new TokenError("unsupported_grant_type", $"Grant type '{request.GrantType}' is not supported"))
            };

            var successful = result is OkObjectResult;
            await LogAttemptAsync(request, successful, successful ? null : "unsupported_grant_type");
            if (successful)
                logger.LogInformation("Issued access token for client {ClientId} (grant {GrantType})",
                    request.ClientId, request.GrantType);
            else
                logger.LogWarning("Token request from client {ClientId} rejected: unsupported grant type {GrantType}",
                    request.ClientId, request.GrantType);
            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            await LogAttemptAsync(request, false, ex.Message);
            logger.LogWarning("Token request from client {ClientId} failed authentication: {Reason}",
                request.ClientId, ex.Message);
            return InvalidClient(ex.Message);
        }
        catch (InvalidScopeException ex)
        {
            await LogAttemptAsync(request, false, ex.Message);
            logger.LogWarning("Token request from client {ClientId} rejected: {Reason}",
                request.ClientId, ex.Message);
            return BadRequest(new TokenError("invalid_scope", ex.Message));
        }
    }

    private IActionResult InvalidClient(string description)
    {
        Response.Headers.WWWAuthenticate = $"Basic realm=\"{AuthorizationHeader.Realm}\", charset=\"UTF-8\"";
        return Unauthorized(new TokenError("invalid_client", description));
    }

    private string? DuplicateParameter() =>
        Request.HasFormContentType
            ? RecognizedParameters.FirstOrDefault(name => Request.Form[name].Count > 1)
            : null;

    private Task LogAttemptAsync(TokenRequest request, bool successful, string? failureReason)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.FirstOrDefault();
        return authAttemptStore.LogAsync(request.ClientId, request.GrantType ?? string.Empty, successful, failureReason, ip, userAgent);
    }

    private bool TryResolveClientCredentials(TokenRequest request, out string? error)
    {
        error = null;
        var basic = Request.BasicCredentials();

        if (basic is not null && !string.IsNullOrEmpty(request.ClientSecret))
        {
            error = "More than one client authentication mechanism in a single request";
            return false;
        }

        if (basic is not null)
        {
            if (!string.IsNullOrEmpty(request.ClientId) && request.ClientId != basic.Value.Id)
            {
                error = "client_id does not match the authenticated client";
                return false;
            }

            request.ClientId = basic.Value.Id;
            request.ClientSecret = basic.Value.Secret;
        }

        return true;
    }
}
