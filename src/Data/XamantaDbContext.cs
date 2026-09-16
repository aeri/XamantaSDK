using Microsoft.EntityFrameworkCore;
using XamantaSDK.Data.Entities;
using PolicyEntity = XamantaSDK.Data.Entities.Policy;

namespace XamantaSDK.Data;

public class XamantaDbContext(DbContextOptions<XamantaDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<IssuedToken> IssuedTokens => Set<IssuedToken>();
    public DbSet<AuthAttempt> AuthAttempts => Set<AuthAttempt>();
    public DbSet<PolicyEntity> Policies => Set<PolicyEntity>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ComplianceReport> ComplianceReports => Set<ComplianceReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(b =>
        {
            b.HasKey(c => c.ClientId);
            b.Property(c => c.ClientId).HasMaxLength(128);
            b.Property(c => c.ClientSecret).IsRequired().HasMaxLength(256);
            b.Property(c => c.AllowedScopes).HasMaxLength(512);
            b.Property(c => c.EnrollmentId).HasMaxLength(64);
            b.HasOne(c => c.Enrollment)
                .WithMany()
                .HasForeignKey(c => c.EnrollmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Enrollment>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).HasMaxLength(64);
            b.Property(e => e.Selector).IsRequired().HasMaxLength(64);
            b.Property(e => e.VerifierHash).IsRequired().HasMaxLength(256);
            b.Property(e => e.AllowedScopes).HasMaxLength(512);
            b.HasIndex(e => e.Selector).IsUnique();
            b.Ignore(e => e.IsRevoked);
            b.Ignore(e => e.IsExpired);
            b.Ignore(e => e.IsExhausted);
            b.Ignore(e => e.IsActive);
        });

        modelBuilder.Entity<IssuedToken>(b =>
        {
            b.HasKey(t => t.Jti);
            b.Property(t => t.Jti).HasMaxLength(64);
            b.Property(t => t.ClientId).IsRequired().HasMaxLength(128);
            b.Property(t => t.GrantType).IsRequired().HasMaxLength(32);
            b.Property(t => t.Scope).HasMaxLength(512);
            b.HasIndex(t => t.ClientId);
            b.HasIndex(t => t.IssuedAt);
            b.HasOne(t => t.Client)
                .WithMany()
                .HasForeignKey(t => t.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuthAttempt>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.ClientId).HasMaxLength(128);
            b.Property(a => a.GrantType).HasMaxLength(32);
            b.Property(a => a.FailureReason).HasMaxLength(256);
            b.Property(a => a.IpAddress).HasMaxLength(64);
            b.Property(a => a.UserAgent).HasMaxLength(512);
            b.HasIndex(a => a.AttemptedAt);
            b.HasIndex(a => a.ClientId);
        });

        modelBuilder.Entity<PolicyEntity>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).IsRequired().HasMaxLength(128);
            b.Property(p => p.Data).IsRequired();
            b.HasIndex(p => new { p.Name, p.Version }).IsUnique();
        });

        modelBuilder.Entity<Device>(b =>
        {
            b.HasKey(d => d.DeviceId);
            b.Property(d => d.DeviceId).HasMaxLength(128);
            b.Property(d => d.PolicyName).HasMaxLength(128);
            b.Property(d => d.LastSentPolicyName).HasMaxLength(128);
            b.Property(d => d.AppliedPolicyName).HasMaxLength(128);
        });

        modelBuilder.Entity<ComplianceReport>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.DeviceId).IsRequired().HasMaxLength(128);
            b.Property(r => r.ExecutionId).HasMaxLength(64);
            b.Property(r => r.PolicyName).HasMaxLength(128);
            b.Property(r => r.Data).IsRequired();
            b.HasIndex(r => new { r.DeviceId, r.ReceivedAt });
        });
    }
}
