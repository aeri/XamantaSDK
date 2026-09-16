namespace XamantaSDK.Data.Entities;

public class Enrollment
{
    public string Id { get; set; } = string.Empty;
    public string Selector { get; set; } = string.Empty;
    public string VerifierHash { get; set; } = string.Empty;
    public string? AllowedScopes { get; set; }
    public int MaxActivations { get; set; } = 1;
    public int ActivationCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
    public bool IsExhausted => ActivationCount >= MaxActivations;
    public bool IsActive => !IsRevoked && !IsExpired && !IsExhausted;
}
