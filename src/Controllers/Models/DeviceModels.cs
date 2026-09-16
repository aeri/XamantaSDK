using System.Text.Json;
using XamantaSDK.OpenApi;

namespace XamantaSDK.Controllers.Models;

/// <summary>Body of a policy assignment.</summary>
/// <param name="PolicyName">
/// Name of a policy that has already been published. Assigning an unpublished name is refused,
/// so a device is never left pointing at a policy that does not exist.
/// </param>
public record AssignPolicyRequest(string PolicyName);

/// <summary>
/// Server-side status of one device: connectivity, the policy it should run, the policy it says
/// it is running, and the identity it last reported.
/// </summary>
/// <param name="DeviceId">Stable device identity — the OAuth <c>client_id</c>, which is the Android ID.</param>
/// <param name="Connected">Whether the device currently holds an open policy stream on this server instance.</param>
/// <param name="PolicyName">Effective policy name: the explicit assignment, or <c>default</c> when unassigned.</param>
/// <param name="FirstSeenAt">First time the device ever synced (UTC).</param>
/// <param name="LastSyncAt">Last time the device opened a policy stream (UTC).</param>
/// <param name="LastContactAt">Last time the device was heard from at all, sync or compliance report (UTC).</param>
/// <param name="LastSentPolicyName">Name of the last policy pushed to the device.</param>
/// <param name="LastSentPolicyVersion">Version of the last policy pushed to the device.</param>
/// <param name="AppliedPolicyName">Name of the policy the device's last compliance report was about.</param>
/// <param name="AppliedPolicyVersion">Version of the policy the device's last compliance report was about.</param>
/// <param name="ComplianceStatus">
/// Last reported <c>policy.Compliance</c> value by name — <c>IN_FORCE</c>, <c>PENDING_COMPLIANCE</c>
/// or <c>DEFIANCE</c>. Null until the device has reported once.
/// </param>
/// <param name="LastComplianceAt">When that report arrived (UTC).</param>
/// <param name="SyncInfo">The device's last <c>policy.SyncRequest</c>: build, DPC version and signer.</param>
/// <param name="Registration">What the device declared at registration. Null when its client row has been removed.</param>
public record DeviceView(
    string DeviceId,
    bool Connected,
    string PolicyName,
    DateTime FirstSeenAt,
    DateTime? LastSyncAt,
    DateTime? LastContactAt,
    string? LastSentPolicyName,
    int? LastSentPolicyVersion,
    string? AppliedPolicyName,
    int? AppliedPolicyVersion,
    string? ComplianceStatus,
    DateTime? LastComplianceAt,
    [property: ProtoSchema(typeof(SyncRequest))] JsonElement? SyncInfo,
    DeviceRegistrationView? Registration);

/// <summary>Descriptive data captured when the device registered. Never used for identity.</summary>
/// <param name="RegisteredAt">When the device completed dynamic registration (UTC).</param>
/// <param name="EnrollmentId">Enrollment whose token was spent to register. Null once that enrollment is deleted.</param>
/// <param name="Metadata">
/// The RFC 7591 metadata document as registered, including the values the server provisioned
/// itself: <c>android_id</c>, <c>client_name</c>, <c>grant_types</c>, <c>scope</c> and the
/// hardware descriptors.
/// </param>
public record DeviceRegistrationView(
    DateTime RegisteredAt,
    string? EnrollmentId,
    JsonElement? Metadata);

/// <summary>One compliance report received from a device.</summary>
/// <param name="ExecutionId">Device-generated id of the apply run this report describes.</param>
/// <param name="Compliance">The reported <c>policy.Compliance</c> value by name.</param>
/// <param name="PolicyName">Policy the report is about, correlated server-side from the last policy sent.</param>
/// <param name="PolicyVersion">Version of that policy.</param>
/// <param name="ReceivedAt">When the server stored the report (UTC).</param>
/// <param name="Report">The full <c>policy.ApplyPolicyResponse</c>, including every non-compliance detail.</param>
public record ComplianceReportView(
    string ExecutionId,
    string Compliance,
    string? PolicyName,
    int? PolicyVersion,
    DateTime ReceivedAt,
    [property: ProtoSchema(typeof(ApplyPolicyResponse))] JsonElement? Report);
