using Grpc.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using XamantaSDK.Data.Stores;

namespace XamantaSDK.Services;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PolicyGrpcService(
    ILogger<PolicyGrpcService> logger,
    DeviceConnectionRegistry registry,
    PolicyDispatcher dispatcher,
    DeviceStore devices,
    PolicyStore policies) : PolicySyncService.PolicySyncServiceBase
{
    public override async Task SyncPolicy(
        SyncRequest request,
        IServerStreamWriter<ApplyPolicyRequest> responseStream,
        ServerCallContext context)
    {
        var deviceId = context.DeviceId();
        var device = await devices.UpsertFromSyncAsync(deviceId, request);

        var connectedAt = DateTimeOffset.UtcNow;
        var channel = registry.Policy.Register(deviceId);
        logger.LogInformation(
            "Device {DeviceId} connected to policy stream: {Manufacturer} {Model}, Android {AndroidVersion} (API {ApiLevel}), security patch {SecurityPatch}, DPC {PackageName} v{VersionName} ({VersionCode}), signer {SigningSha256}",
            deviceId, request.Manufacturer, request.Model, request.AndroidVersion, request.ApiLevel,
            request.SecurityPatch, request.PackageName, request.VersionName, request.VersionCode, request.SigningSha256);

        var policyName = device.PolicyName ?? PolicyStore.DefaultName;
        var policy = await policies.GetLatestAsync(policyName);
        logger.LogInformation("Effective policy for device {DeviceId} on sync: {PolicyName} ({Assignment}), {Outcome}",
            deviceId, policyName, device.PolicyName is null ? "default fallback" : "assigned",
            policy is null ? "none published yet" : $"v{policy.Version}");

        if (policy is not null)
            await dispatcher.TryPushAsync(deviceId, policy, "sync");

        try
        {
            await foreach (var msg in channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(msg, context.CancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            registry.Policy.Unregister(deviceId, channel);
            logger.LogInformation("Device {DeviceId} disconnected from policy stream after {DurationSeconds:F0}s",
                deviceId, (DateTimeOffset.UtcNow - connectedAt).TotalSeconds);
        }
    }

    public override async Task<ComplianceAck> ReportCompliance(ApplyPolicyResponse request, ServerCallContext context)
    {
        var deviceId = context.DeviceId();
        var report = await devices.RecordComplianceAsync(deviceId, request);

        logger.LogInformation(
            "Compliance report from device {DeviceId} for policy {PolicyName} v{PolicyVersion} (execution {ExecutionId}): compliance={Compliance}, details={DetailCount}, appliedAt={AppliedAt}",
            deviceId, report.PolicyName, report.PolicyVersion, request.ExecutionId,
            request.Compliance, request.Details.Count, request.AppliedAt);

        foreach (var detail in request.Details)
        {
            logger.LogWarning(
                "NonCompliance [{ExecutionId}] on device {DeviceId}: setting={Setting}, reason={Reason}, package={Package}, field={Field}, current={Current}",
                request.ExecutionId, deviceId, detail.SettingName, detail.NonComplianceReason,
                detail.PackageName, detail.FieldPath, detail.CurrentValue);
        }

        return new ComplianceAck { Received = true };
    }
}
