using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using XamantaSDK.Infrastructure;

namespace XamantaSDK.Auth;

public static class AdminApiKeyDefaults
{
    public const string AuthenticationScheme = "AdminApiKey";

    public const string HeaderName = "X-Admin-Api-Key";
}

public sealed class AdminApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configured = configuration["Admin:ApiKey"];
        if (string.IsNullOrEmpty(configured))
            return Task.FromResult(AuthenticateResult.Fail("Admin API key is not configured."));

        var presented = Request.Headers[AdminApiKeyDefaults.HeaderName].ToString();
        if (string.IsNullOrEmpty(presented))
            return Task.FromResult(AuthenticateResult.Fail("Admin API key is missing."));

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(configured));
        if (!matches)
            return Task.FromResult(AuthenticateResult.Fail("Invalid admin API key."));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(
            new ApiError($"Admin API key required in the {AdminApiKeyDefaults.HeaderName} header."),
            JsonSerializerOptions.Web);
        await Response.WriteAsync(body);
    }
}
