namespace XamantaSDK.Data.Entities;

public class AuthAttempt
{
    public long Id { get; set; }
    public string? ClientId { get; set; }
    public string GrantType { get; set; } = string.Empty;
    public bool Successful { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}
