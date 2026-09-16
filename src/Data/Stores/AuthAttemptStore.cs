using XamantaSDK.Data.Entities;

namespace XamantaSDK.Data.Stores;

public class AuthAttemptStore(XamantaDbContext db)
{
    public async Task LogAsync(string? clientId, string grantType, bool successful, string? failureReason, string? ipAddress, string? userAgent)
    {
        db.AuthAttempts.Add(new AuthAttempt
        {
            ClientId = clientId,
            GrantType = grantType,
            Successful = successful,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });
        await db.SaveChangesAsync();
    }
}
