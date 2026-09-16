using Microsoft.EntityFrameworkCore;
using XamantaSDK.Data.Entities;
using XamantaSDK.Infrastructure;

namespace XamantaSDK.Data.Stores;

public class DeviceStore(XamantaDbContext db)
{
    public async Task<Device> UpsertFromSyncAsync(string deviceId, SyncRequest sync)
    {
        var now = DateTime.UtcNow;
        var device = await db.Devices.FindAsync(deviceId);
        if (device is null)
        {
            device = new Device { DeviceId = deviceId, FirstSeenAt = now };
            db.Devices.Add(device);
        }

        device.SyncInfoJson = ProtoJson.Formatter.Format(sync);
        device.LastSyncAt = now;
        device.LastContactAt = now;
        await db.SaveChangesAsync();
        return device;
    }

    public Task TouchContactAsync(string deviceId) =>
        UpdateAsync(deviceId, d => d.LastContactAt = DateTime.UtcNow);

    public Task RecordPolicySentAsync(string deviceId, string policyName, int version) =>
        UpdateAsync(deviceId, d =>
        {
            d.LastSentPolicyName = policyName;
            d.LastSentPolicyVersion = version;
        });

    public Task<bool> AssignPolicyAsync(string deviceId, string policyName) =>
        UpdateAsync(deviceId, d => d.PolicyName = policyName);

    public async Task<ComplianceReport> RecordComplianceAsync(string deviceId, ApplyPolicyResponse response)
    {
        var now = DateTime.UtcNow;
        var device = await db.Devices.FindAsync(deviceId);

        var report = new ComplianceReport
        {
            DeviceId = deviceId,
            ExecutionId = response.ExecutionId,
            Compliance = (int)response.Compliance,
            PolicyName = device?.LastSentPolicyName,
            PolicyVersion = device?.LastSentPolicyVersion,
            Data = ProtoJson.Formatter.Format(response),
            ReceivedAt = now
        };
        db.ComplianceReports.Add(report);

        if (device is not null)
        {
            device.LastComplianceStatus = (int)response.Compliance;
            device.LastComplianceAt = now;
            device.LastContactAt = now;
            if (response.Compliance == Compliance.InForce)
            {
                device.AppliedPolicyName = device.LastSentPolicyName;
                device.AppliedPolicyVersion = device.LastSentPolicyVersion;
            }
        }

        await db.SaveChangesAsync();
        return report;
    }

    public async Task<bool> DeleteAsync(string deviceId)
    {
        var device = await db.Devices.FindAsync(deviceId);
        var client = await db.Clients.FindAsync(deviceId);
        var reports = await db.ComplianceReports.Where(r => r.DeviceId == deviceId).ToListAsync();

        if (device is null && client is null && reports.Count == 0)
            return false;

        if (device is not null)
            db.Devices.Remove(device);
        if (client is not null)
            db.Clients.Remove(client);
        db.ComplianceReports.RemoveRange(reports);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        return true;
    }

    public async Task<Device?> GetAsync(string deviceId) => await db.Devices.FindAsync(deviceId);

    public async Task<IReadOnlyList<Device>> ListAsync() =>
        await db.Devices.OrderByDescending(d => d.LastContactAt).ToListAsync();

    public async Task<IReadOnlyList<ComplianceReport>> ListReportsAsync(string deviceId, int limit) =>
        await db.ComplianceReports
            .Where(r => r.DeviceId == deviceId)
            .OrderByDescending(r => r.ReceivedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync();

    public async Task<IReadOnlyList<string>> ListDeviceIdsWithEffectivePolicyAsync(string policyName)
    {
        var query = policyName == PolicyStore.DefaultName
            ? db.Devices.Where(d => d.PolicyName == null || d.PolicyName == policyName)
            : db.Devices.Where(d => d.PolicyName == policyName);

        return await query.Select(d => d.DeviceId).ToListAsync();
    }

    private async Task<bool> UpdateAsync(string deviceId, Action<Device> mutate)
    {
        var device = await db.Devices.FindAsync(deviceId);
        if (device is null)
            return false;

        mutate(device);
        await db.SaveChangesAsync();
        return true;
    }
}
