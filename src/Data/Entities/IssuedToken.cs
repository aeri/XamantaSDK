namespace XamantaSDK.Data.Entities;

public class IssuedToken
{
    public string Jti { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Scope { get; set; }
    public string GrantType { get; set; } = string.Empty;

    public Client? Client { get; set; }
}
