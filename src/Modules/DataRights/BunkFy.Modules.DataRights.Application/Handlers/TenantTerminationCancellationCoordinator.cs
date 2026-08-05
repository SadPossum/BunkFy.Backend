namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

internal sealed class TenantTerminationCancellationCoordinator(
    IEnumerable<ITenantTerminationContributor> contributors)
{
    public static Result Request(
        TenantTerminationProcess process,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(process);
        return process.RequestCancellation(
            expectedVersion,
            actorId,
            nowUtc);
    }

    public Result Complete(
        TenantTerminationProcess process,
        IReadOnlyCollection<TenantTerminationOwnerWorkItem> ownerWorkItems,
        long operationRevision,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(ownerWorkItems);

        Result<IReadOnlyList<ITenantTerminationContributor>> ordered =
            TenantTerminationContributorSet.OrderForPhase(
                contributors,
                TenantTerminationContributionPhase.Restore);
        if (ordered.IsFailure ||
            !HasExactProof(
                process,
                ownerWorkItems,
                ordered.Value,
                operationRevision))
        {
            return Result.Failure(
                DataRightsApplicationErrors
                    .TenantTerminationCancellationProofInvalid);
        }

        return process.CompleteCancellation(
            operationRevision,
            expectedVersion,
            actorId,
            nowUtc);
    }

    private static bool HasExactProof(
        TenantTerminationProcess process,
        IReadOnlyCollection<TenantTerminationOwnerWorkItem> ownerWorkItems,
        IReadOnlyCollection<ITenantTerminationContributor> contributors,
        long operationRevision)
    {
        if (process.Phase != TenantTerminationProcessPhase.Restore ||
            process.Status != TenantTerminationProcessStatus.Running ||
            operationRevision != process.OperationRevision ||
            ownerWorkItems.Count != contributors.Count)
        {
            return false;
        }

        Dictionary<string, TenantTerminationOwnerWorkItem> workByOwner =
            ownerWorkItems
                .Where(workItem => workItem is not null)
                .GroupBy(workItem => workItem.OwnerKey, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(
                    group => group.Key,
                    group => group.Single(),
                    StringComparer.Ordinal);
        if (workByOwner.Count != ownerWorkItems.Count)
        {
            return false;
        }

        return contributors.All(contributor =>
        {
            TenantTerminationContributorDescriptor descriptor =
                contributor.Descriptor;
            return workByOwner.TryGetValue(
                    descriptor.OwnerKey,
                    out TenantTerminationOwnerWorkItem? workItem) &&
                workItem.ProcessId == process.Id &&
                workItem.CaseId == process.CaseId &&
                workItem.ApprovalRevision == process.ApprovalRevision &&
                workItem.OperationRevision == operationRevision &&
                workItem.TerminationEpoch == process.TerminationEpoch &&
                workItem.Phase == TenantTerminationOwnerPhase.Restore &&
                workItem.OwnerContractVersion == descriptor.ContractVersion &&
                workItem.CatalogVersion == descriptor.CatalogVersion &&
                string.Equals(
                    workItem.CatalogSha256,
                    descriptor.CatalogSha256,
                    StringComparison.Ordinal) &&
                string.Equals(
                    workItem.PolicyEvidenceSha256,
                    process.PolicyEvidenceSha256,
                    StringComparison.Ordinal) &&
                workItem.State == TenantTerminationOwnerWorkState.Completed &&
                workItem.RemainingActiveCount == 0 &&
                workItem.SelectedProofRevision is >= 0 &&
                workItem.ResultingProofRevision >=
                    workItem.SelectedProofRevision &&
                workItem.ResultRecordedAtUtc.HasValue;
        });
    }
}
