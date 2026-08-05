namespace BunkFy.Modules.DataRights.Application.Ports;

using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Naming;

public sealed record TenantTerminationReplayIntent(
    int ContractVersion,
    string TenantId,
    Guid ProcessId,
    Guid CaseId,
    DataRightsRequesterRelationship RequesterRelationship,
    string RequestedBy,
    DateTimeOffset RequestedAtUtc,
    bool ExportRequested,
    long ApprovalRevision,
    string ApprovedBy,
    DateTimeOffset ApprovedAtUtc,
    string PolicyEvidenceSha256,
    string ApprovedOwnerCatalogSha256,
    Guid IdempotencyKey,
    Guid TerminationEpoch,
    string ExecutingActorId,
    DateTimeOffset ExecutionStartedAtUtc,
    DateTimeOffset RecordedAtUtc,
    string IntentSha256)
{
    public const int CurrentContractVersion = 1;

    public static TenantTerminationReplayIntent Create(
        string tenantId,
        Guid processId,
        Guid caseId,
        DataRightsRequesterRelationship requesterRelationship,
        string requestedBy,
        DateTimeOffset requestedAtUtc,
        bool exportRequested,
        long approvalRevision,
        string approvedBy,
        DateTimeOffset approvedAtUtc,
        string policyEvidenceSha256,
        string approvedOwnerCatalogSha256,
        Guid idempotencyKey,
        Guid terminationEpoch,
        string executingActorId,
        DateTimeOffset executionStartedAtUtc,
        DateTimeOffset recordedAtUtc)
    {
        string normalizedTenant = TenantIds.TryNormalize(
            tenantId,
            out string? parsedTenant)
                ? parsedTenant
                : string.Empty;
        TenantTerminationReplayIntent intent = new(
            CurrentContractVersion,
            normalizedTenant,
            processId,
            caseId,
            requesterRelationship,
            requestedBy?.Trim() ?? string.Empty,
            requestedAtUtc,
            exportRequested,
            approvalRevision,
            approvedBy?.Trim() ?? string.Empty,
            approvedAtUtc,
            policyEvidenceSha256?.Trim() ?? string.Empty,
            approvedOwnerCatalogSha256?.Trim() ?? string.Empty,
            idempotencyKey,
            terminationEpoch,
            executingActorId?.Trim() ?? string.Empty,
            executionStartedAtUtc,
            recordedAtUtc,
            IntentSha256: string.Empty);
        intent = intent with
        {
            IntentSha256 =
                TenantTerminationReplayProof.ComputeIntentSha256(intent)
        };
        return intent.HasValidProof()
            ? intent
            : throw new ArgumentException(
                "The tenant-termination replay intent is invalid.",
                nameof(tenantId));
    }

    public bool HasValidProof() =>
        this.ContractVersion == CurrentContractVersion &&
        TenantIds.TryNormalize(this.TenantId, out string? normalizedTenant) &&
        string.Equals(
            normalizedTenant,
            this.TenantId,
            StringComparison.Ordinal) &&
        this.ProcessId != Guid.Empty &&
        this.CaseId != Guid.Empty &&
        this.RequesterRelationship is
            DataRightsRequesterRelationship.ControllerInitiated or
            DataRightsRequesterRelationship.TenantOwner &&
        TenantTerminationReplayProof.IsActor(this.RequestedBy) &&
        this.RequestedAtUtc != default &&
        this.ApprovalRevision > 0 &&
        TenantTerminationReplayProof.IsActor(this.ApprovedBy) &&
        this.ApprovedAtUtc >= this.RequestedAtUtc &&
        TenantTerminationReplayProof.IsSha256(this.PolicyEvidenceSha256) &&
        TenantTerminationReplayProof.IsSha256(
            this.ApprovedOwnerCatalogSha256) &&
        this.IdempotencyKey != Guid.Empty &&
        this.TerminationEpoch != Guid.Empty &&
        TenantTerminationReplayProof.IsActor(this.ExecutingActorId) &&
        !string.Equals(
            this.ApprovedBy,
            this.ExecutingActorId,
            StringComparison.Ordinal) &&
        this.ExecutionStartedAtUtc >= this.ApprovedAtUtc &&
        this.RecordedAtUtc >= this.ExecutionStartedAtUtc &&
        TenantTerminationReplayProof.FixedTimeSha256Equals(
            this.IntentSha256,
            TenantTerminationReplayProof.ComputeIntentSha256(this));
}

public static partial class TenantTerminationReplayProof
{
    private const string IntentDomain =
        "bunkfy.data-rights.tenant-termination.replay-intent.v1";
    private const string IntentLogicalEntryDomain =
        "bunkfy.data-rights.tenant-termination.replay-intent-entry.v1";

    public static string ComputeIntentSha256(
        TenantTerminationReplayIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        StringBuilder canonical = new();
        Append(canonical, IntentDomain);
        Append(canonical, intent.ContractVersion);
        Append(canonical, intent.TenantId);
        Append(canonical, intent.ProcessId);
        Append(canonical, intent.CaseId);
        Append(canonical, (int)intent.RequesterRelationship);
        Append(canonical, intent.RequestedBy);
        Append(canonical, intent.RequestedAtUtc);
        Append(canonical, intent.ExportRequested ? 1 : 0);
        Append(canonical, intent.ApprovalRevision);
        Append(canonical, intent.ApprovedBy);
        Append(canonical, intent.ApprovedAtUtc);
        Append(canonical, intent.PolicyEvidenceSha256);
        Append(canonical, intent.ApprovedOwnerCatalogSha256);
        Append(canonical, intent.IdempotencyKey);
        Append(canonical, intent.TerminationEpoch);
        Append(canonical, intent.ExecutingActorId);
        Append(canonical, intent.ExecutionStartedAtUtc);
        Append(canonical, intent.RecordedAtUtc);
        return Hash(canonical);
    }

    public static string ComputeIntentLogicalEntryId(
        string tenantId,
        Guid processId)
    {
        StringBuilder canonical = new();
        Append(canonical, IntentLogicalEntryDomain);
        Append(canonical, NormalizeTenant(tenantId));
        Append(canonical, processId);
        Append(canonical, (int)TenantTerminationReplayEntryKind.Intent);
        return Hash(canonical);
    }
}
