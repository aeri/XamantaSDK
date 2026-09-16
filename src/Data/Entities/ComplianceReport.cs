namespace XamantaSDK.Data.Entities;

public class ComplianceReport
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public int Compliance { get; set; }
    public string? PolicyName { get; set; }
    public int? PolicyVersion { get; set; }
    public string Data { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
