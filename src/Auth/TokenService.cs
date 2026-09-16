using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using XamantaSDK.Auth.Models;
using XamantaSDK.Data.Stores;

namespace XamantaSDK.Auth;

public class TokenService(
    ClientStore clientStore,
    IssuedTokenStore issuedTokenStore,
    IConfiguration configuration)
{
    private const int DefaultAccessTokenLifetime = 3600;

    public async Task<TokenResponse> HandleClientCredentialsAsync(string clientId, string clientSecret, string? scope)
    {
        var client = await clientStore.FindByCredentialsAsync(clientId, clientSecret)
            ?? throw new UnauthorizedAccessException("Invalid client credentials");

        if (!Scopes.TryResolve(client.AllowedScopes, scope, out var granted, out var disallowed))
            throw new InvalidScopeException($"Scope not allowed for this client: {disallowed}");

        var (accessToken, jti, issuedAt, expiresAt) = GenerateAccessToken(client.ClientId, granted);
        await issuedTokenStore.LogAsync(jti, client.ClientId, issuedAt, expiresAt, granted, "client_credentials");

        return new TokenResponse(accessToken, "Bearer", (int)(expiresAt - issuedAt).TotalSeconds, granted);
    }

    private (string Token, string Jti, DateTime IssuedAt, DateTime ExpiresAt) GenerateAccessToken(string clientId, string? scope)
    {
        var jwtKey = configuration["OAuth:JwtSigningKey"]
            ?? throw new InvalidOperationException("JWT signing key is not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var lifetime = configuration.GetValue("OAuth:AccessTokenLifetime", DefaultAccessTokenLifetime);

        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddSeconds(lifetime);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("client_id", clientId)
        };

        if (!string.IsNullOrWhiteSpace(scope))
            claims.Add(new("scope", scope));

        var token = new JwtSecurityToken(
            issuer: "XamantaSDK",
            audience: "XamantaSDK",
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        token.Header["typ"] = "at+jwt";

        return (new JwtSecurityTokenHandler().WriteToken(token), jti, issuedAt, expiresAt);
    }
}
