namespace BunkFy.Modules.DataRights.Application.Models;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;

internal static class TenantTerminationVerificationProofSet
{
    private const string DigestDomain =
        "bunkfy.data-rights.tenant-termination.verification-proof-set.v1";

    public static string ComputeSha256(
        TenantTerminationProcess process,
        IReadOnlyCollection<TenantTerminationOwnerWorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(workItems);
        if (!process.DestroyCompletedOperationRevision.HasValue ||
            process.FreezeOperationRevision is not > 0 ||
            process.WorkspaceFenceRevision is not > 0 ||
            !TenantTerminationReplayProof.IsSha256(
                process.FrozenRevisionSha256) ||
            workItems.Count is <= 0 or >
                TenantTerminationContract.MaximumContributors)
        {
            throw InvalidProofSet();
        }

        long destroyOperationRevision =
            process.DestroyCompletedOperationRevision.Value;

        TenantTerminationOwnerWorkItem[] ordered = workItems
            .OrderBy(item => item.OwnerKey, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToArray();
        if (ordered.Select(item => item.OwnerKey)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length ||
            ordered.Any(item => !HasValidProof(
                process,
                item,
                destroyOperationRevision)))
        {
            throw InvalidProofSet();
        }

        StringBuilder canonical = new();
        Append(canonical, DigestDomain);
        Append(canonical, process.ScopeId);
        Append(canonical, process.Id);
        Append(canonical, process.CaseId);
        Append(canonical, process.ApprovalRevision);
        Append(canonical, process.FreezeOperationRevision!.Value);
        Append(canonical, process.WorkspaceFenceRevision!.Value);
        Append(canonical, destroyOperationRevision);
        Append(canonical, process.TerminationEpoch);
        Append(canonical, process.PolicyEvidenceSha256);
        Append(canonical, process.FrozenRevisionSha256!);
        Append(canonical, ordered.Length);
        foreach (TenantTerminationOwnerWorkItem item in ordered)
        {
            Append(canonical, item.Id);
            Append(canonical, item.IdempotencyKey);
            Append(canonical, item.OwnerKey);
            Append(canonical, item.OwnerContractVersion);
            Append(canonical, item.CatalogVersion);
            Append(canonical, item.CatalogSha256);
            Append(canonical, item.TaskRunId!.Value);
            Append(canonical, item.LastTaskAttempt);
            Append(canonical, item.ResultCode!);
            Append(canonical, item.AffectedCount!.Value);
            Append(canonical, item.RetainedMinimumCount!.Value);
            Append(canonical, item.RemainingActiveCount!.Value);
            Append(canonical, item.SelectedProofRevision!.Value);
            Append(canonical, item.ResultingProofRevision!.Value);
            Append(canonical, item.ResultRecordedAtUtc!.Value);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public static bool MatchesWorkItem(
        TenantTerminationOwnerWorkItem workItem,
        TenantTerminationContributionResult contribution)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(contribution);
        return workItem.State == TenantTerminationOwnerWorkState.Completed &&
            contribution.Status ==
                TenantTerminationContributionStatus.Completed &&
            string.Equals(
                workItem.ResultCode,
                contribution.ResultCode,
                StringComparison.Ordinal) &&
            workItem.AffectedCount == contribution.AffectedCount &&
            workItem.RetainedMinimumCount ==
                contribution.RetainedMinimumCount &&
            workItem.RemainingActiveCount ==
                contribution.RemainingActiveCount &&
            workItem.HoldReviewAtUtc == contribution.HoldReviewAtUtc &&
            workItem.SelectedProofRevision ==
                contribution.SelectedProofRevision &&
            workItem.ResultingProofRevision ==
                contribution.ResultingProofRevision &&
            workItem.CatalogVersion == contribution.CatalogVersion &&
            TenantTerminationReplayProof.FixedTimeSha256Equals(
                workItem.CatalogSha256,
                contribution.CatalogSha256) &&
            workItem.ResultRecordedAtUtc == contribution.RecordedAtUtc;
    }

    private static bool HasValidProof(
        TenantTerminationProcess process,
        TenantTerminationOwnerWorkItem item,
        long destroyOperationRevision) =>
        string.Equals(item.ScopeId, process.ScopeId, StringComparison.Ordinal) &&
        item.ProcessId == process.Id &&
        item.CaseId == process.CaseId &&
        item.ApprovalRevision == process.ApprovalRevision &&
        item.OperationRevision == destroyOperationRevision &&
        item.TerminationEpoch == process.TerminationEpoch &&
        item.Phase == TenantTerminationOwnerPhase.Destroy &&
        item.State == TenantTerminationOwnerWorkState.Completed &&
        item.OwnerContractVersion == TenantTerminationContract.CurrentVersion &&
        TenantTerminationReplayProof.IsStableCode(
            item.OwnerKey,
            TenantTerminationContract.OwnerKeyMaxLength) &&
        item.CatalogVersion > 0 &&
        TenantTerminationReplayProof.FixedTimeSha256Equals(
            item.PolicyEvidenceSha256,
            process.PolicyEvidenceSha256) &&
        TenantTerminationReplayProof.IsSha256(item.CatalogSha256) &&
        item.TaskRunId.HasValue &&
        item.TaskRunId.Value != Guid.Empty &&
        item.LastTaskAttempt > 0 &&
        TenantTerminationReplayProof.IsStableCode(
            item.ResultCode,
            TenantTerminationContract.ResultCodeMaxLength) &&
        item.AffectedCount is >= 0 &&
        item.RetainedMinimumCount is >= 0 &&
        item.RemainingActiveCount == 0 &&
        !item.HoldReviewAtUtc.HasValue &&
        item.SelectedProofRevision is >= 0 &&
        item.ResultingProofRevision >= item.SelectedProofRevision &&
        item.ResultRecordedAtUtc.HasValue;

    private static InvalidOperationException InvalidProofSet() =>
        new("DataRights.TenantTerminationVerificationProofSetInvalid");

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static void Append(StringBuilder target, Guid value) =>
        Append(target, value.ToString("N"));

    private static void Append(StringBuilder target, long value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, int value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(
        StringBuilder target,
        DateTimeOffset value) =>
        Append(
            target,
            value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}
