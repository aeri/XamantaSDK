namespace XamantaSDK.Data.Entities;

public class Client
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string? AllowedScopes { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public string? EnrollmentId { get; set; }
    public Enrollment? Enrollment { get; set; }
}
