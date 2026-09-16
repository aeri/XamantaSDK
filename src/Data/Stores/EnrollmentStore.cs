using Microsoft.EntityFrameworkCore;
using XamantaSDK.Auth;
using XamantaSDK.Data.Entities;

namespace XamantaSDK.Data.Stores;

public class EnrollmentStore(XamantaDbContext db, SecretProtector protector)
{
    public async Task<(Enrollment Enrollment, string Token)> CreateAsync(
        string? scopes, int maxActivations, DateTime expiresAt)
    {
        var selector = protector.NewToken(16);
        var secret = protector.NewToken();
        var enrollment = new Enrollment
        {
            Id = $"enr_{Guid.NewGuid():N}",
            Selector = selector,
            VerifierHash = protector.Derive(secret),
            AllowedScopes = scopes,
            MaxActivations = maxActivations,
            ExpiresAt = expiresAt
        };

        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();
        return (enrollment, $"{selector}.{secret}");
    }

    public async Task<Enrollment?> FindActiveByTokenAsync(string token)
    {
        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1)
            return null;

        var selector = token[..dot];
        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Selector == selector);
        if (enrollment is null || !protector.Verify(token[(dot + 1)..], enrollment.VerifierHash))
            return null;

        return enrollment.IsActive ? enrollment : null;
    }

    public async Task IncrementActivationAsync(Enrollment enrollment)
    {
        enrollment.ActivationCount++;
        await db.SaveChangesAsync();
    }

    public async Task<bool> RevokeAsync(string id)
    {
        var enrollment = await db.Enrollments.FindAsync(id);
        if (enrollment is null || enrollment.IsRevoked)
            return false;

        enrollment.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<Enrollment>> ListAsync() =>
        await db.Enrollments.OrderByDescending(e => e.CreatedAt).ToListAsync();
}
