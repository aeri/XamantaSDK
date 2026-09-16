using XamantaSDK.Data.Entities;

namespace XamantaSDK.Data.Stores;

public class IssuedTokenStore(XamantaDbContext db)
{
    public async Task LogAsync(string jti, string clientId, DateTime issuedAt, DateTime expiresAt, string? scope, string grantType)
    {
        db.IssuedTokens.Add(new IssuedToken
        {
            Jti = jti,
            ClientId = clientId,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            Scope = scope,
            GrantType = grantType
        });
        await db.SaveChangesAsync();
    }
}
