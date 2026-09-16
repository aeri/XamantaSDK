using Microsoft.EntityFrameworkCore;
using XamantaSDK.Infrastructure;
using PolicyEntity = XamantaSDK.Data.Entities.Policy;

namespace XamantaSDK.Data.Stores;

public class PolicyStore(XamantaDbContext db)
{
    public const string DefaultName = "default";

    public async Task<PolicyEntity> CreateVersionAsync(string name, Policy policy)
    {
        var latest = await db.Policies
            .Where(p => p.Name == name)
            .MaxAsync(p => (int?)p.Version) ?? 0;

        policy.Name = name;
        policy.Version = latest + 1;

        var row = new PolicyEntity
        {
            Name = name,
            Version = policy.Version,
            Data = ProtoJson.Formatter.Format(policy)
        };

        db.Policies.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    public Task<PolicyEntity?> GetLatestAsync(string name) =>
        db.Policies
            .Where(p => p.Name == name)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<PolicyEntity>> ListLatestAsync() =>
        await db.Policies
            .GroupBy(p => p.Name)
            .Select(g => g.OrderByDescending(p => p.Version).First())
            .ToListAsync();

    public async Task<IReadOnlyList<PolicyEntity>> ListVersionsAsync(string name) =>
        await db.Policies
            .Where(p => p.Name == name)
            .OrderByDescending(p => p.Version)
            .ToListAsync();
}
