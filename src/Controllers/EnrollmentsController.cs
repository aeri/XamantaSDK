using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XamantaSDK.Auth;
using XamantaSDK.Auth.Models;
using XamantaSDK.Data.Stores;
using XamantaSDK.Infrastructure;

namespace XamantaSDK.Controllers;

[ApiController]
[Route("enrollments")]
[Tags("Enrollments")]
[Authorize(AuthenticationSchemes = AdminApiKeyDefaults.AuthenticationScheme)]
public class EnrollmentsController(
    EnrollmentStore enrollmentStore,
    ILogger<EnrollmentsController> logger) : ControllerBase
{
    private const int DefaultExpiresInSeconds = 3600;

    /// <summary>Mints an enrollment token.</summary>
    /// <remarks>
    /// The token is the RFC 7591 §3.1 initial access token a device presents to
    /// <c>POST /connect/register</c>. It is returned in the clear exactly once, here: the server
    /// keeps only a lookup selector and a PBKDF2 verifier, so a lost token cannot be recovered.
    ///
    /// The enrollment also fixes what the devices it registers may ever be granted — its
    /// <c>scope</c> is the ceiling a device's registration and every later token request are
    /// narrowed against. It does not fix how long their access tokens live: that is a server
    /// setting, not something a caller chooses.
    ///
    /// Every enrollment expires: an omitted <c>expires_in_seconds</c> gives one hour, and no value
    /// of it means "never".
    ///
    /// The whole body is optional; sending none is the same as sending <c>{}</c>.
    /// </remarks>
    /// <param name="request">Enrollment settings. Omitted fields take their defaults.</param>
    /// <response code="201">Minted. Carries the plaintext token, sent with <c>Cache-Control: no-store</c>.</response>
    /// <response code="400"><c>max_activations</c> or <c>expires_in_seconds</c> was below 1.</response>
    [HttpPost]
    [NoStore]
    [ProducesResponseType<CreateEnrollmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<TokenError>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEnrollmentRequest? request)
    {
        var maxActivations = request?.MaxActivations ?? 1;
        if (maxActivations < 1)
            return BadRequest(new TokenError("invalid_request", "max_activations must be >= 1"));

        var expiresIn = request?.ExpiresInSeconds ?? DefaultExpiresInSeconds;
        if (expiresIn < 1)
            return BadRequest(new TokenError("invalid_request", "expires_in_seconds must be >= 1"));

        var (enrollment, token) = await enrollmentStore.CreateAsync(
            request?.Scope,
            maxActivations,
            DateTime.UtcNow.AddSeconds(expiresIn));

        logger.LogInformation("Created enrollment {EnrollmentId} (maxActivations={MaxActivations}, expiresAt={ExpiresAt})",
            enrollment.Id, enrollment.MaxActivations, enrollment.ExpiresAt);

        return StatusCode(StatusCodes.Status201Created, new CreateEnrollmentResponse(
            enrollment.Id, token, enrollment.MaxActivations, enrollment.ExpiresAt));
    }

    /// <summary>Lists every enrollment, spent and unspent.</summary>
    /// <remarks>
    /// Never includes the token itself. Takes no parameters, and is neither paged nor filtered —
    /// read <c>active</c> to tell which ones a device could still use.
    /// </remarks>
    /// <response code="200">All enrollments.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<EnrollmentView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var items = await enrollmentStore.ListAsync();
        return Ok(items.Select(e => new EnrollmentView(
            e.Id, e.AllowedScopes, e.MaxActivations,
            e.ActivationCount, e.ExpiresAt, e.RevokedAt, e.CreatedAt, e.IsActive)));
    }

    /// <summary>Revokes an enrollment.</summary>
    /// <remarks>
    /// The token stops working for further registrations at once. Devices that already registered
    /// with it keep their credentials — revoking closes the door, it does not unenroll anyone.
    /// Revoking twice is harmless.
    /// </remarks>
    /// <param name="id">Enrollment id, as returned by <c>POST /enrollments</c>.</param>
    /// <response code="204">Revoked.</response>
    /// <response code="404">No enrollment with that id.</response>
    [HttpPost("{id}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(string id)
    {
        if (await enrollmentStore.RevokeAsync(id))
        {
            logger.LogInformation("Revoked enrollment {EnrollmentId}", id);
            return NoContent();
        }

        logger.LogWarning("Cannot revoke unknown enrollment {EnrollmentId}", id);
        return NotFound();
    }
}
