namespace XamantaSDK.Data.Entities;

public class Device
{
    public string DeviceId { get; set; } = string.Empty;
    public string? PolicyName { get; set; }
    public string? SyncInfoJson { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncAt { get; set; }
    public DateTime? LastContactAt { get; set; }
    public string? LastSentPolicyName { get; set; }
    public int? LastSentPolicyVersion { get; set; }
    public string? AppliedPolicyName { get; set; }
    public int? AppliedPolicyVersion { get; set; }
    public int? LastComplianceStatus { get; set; }
    public DateTime? LastComplianceAt { get; set; }
}
