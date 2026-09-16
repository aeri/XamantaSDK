using XamantaSDK.Data.Stores;
using XamantaSDK.Infrastructure;
using PolicyEntity = XamantaSDK.Data.Entities.Policy;

namespace XamantaSDK.Services;

public class PolicyDispatcher(
    DeviceConnectionRegistry registry,
    DeviceStore devices,
    ILogger<PolicyDispatcher> logger)
{
    public async Task<bool> TryPushAsync(string deviceId, PolicyEntity policy, string requestedBy)
    {
        if (!registry.Policy.TryGetWriter(deviceId, out var writer))
            return false;

        if (!writer.TryWrite(ToRequest(policy, requestedBy)))
            return false;

        await devices.RecordPolicySentAsync(deviceId, policy.Name, policy.Version);
        logger.LogInformation("Sent policy {PolicyName} v{Version} ({RequestedBy}) to device {DeviceId}",
            policy.Name, policy.Version, requestedBy, deviceId);
        return true;
    }

    private static ApplyPolicyRequest ToRequest(PolicyEntity policy, string requestedBy) => new()
    {
        Policy = ProtoJson.Parser.Parse<Policy>(policy.Data),
        RequestedBy = requestedBy
    };
}
