namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

internal sealed class TenantTerminationVerificationPlanner(
    IEnumerable<ITenantTerminationContributor> contributors)
{
    private readonly ITenantTerminationContributor[] contributors =
        contributors?.ToArray() ??
        throw new ArgumentNullException(nameof(contributors));

    public Result<TenantTerminationVerificationPlan> Prepare(
        TenantTerminationProcess process,
        IReadOnlyCollection<TenantTerminationOwnerWorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(workItems);
        if (process.Phase != TenantTerminationProcessPhase.Verify ||
            process.Status != TenantTerminationProcessStatus.Running ||
            process.DestroyCompletedOperationRevision is not long
                destroyOperationRevision ||
            destroyOperationRevision <= process.ApprovalRevision ||
            destroyOperationRevision >= process.OperationRevision)
        {
            return Invalid();
        }

        Result<IReadOnlyList<ITenantTerminationContributor>> orderedResult =
            TenantTerminationContributorSet.OrderForPhase(
                this.contributors,
                TenantTerminationContributionPhase.Destroy);
        if (orderedResult.IsFailure)
        {
            return Result.Failure<TenantTerminationVerificationPlan>(
                orderedResult.Error);
        }

        ITenantTerminationContributor[] ordered = orderedResult.Value.ToArray();
        Dictionary<string, TenantTerminationOwnerWorkItem> byOwner =
            new(StringComparer.Ordinal);
        foreach (TenantTerminationOwnerWorkItem? workItem in workItems)
        {
            if (workItem is null ||
                !byOwner.TryAdd(workItem.OwnerKey, workItem))
            {
                return Invalid();
            }
        }

        if (ordered.Length != byOwner.Count ||
            ordered.Any(contributor => !Matches(
                process,
                destroyOperationRevision,
                contributor.Descriptor,
                byOwner)))
        {
            return Invalid();
        }

        HashSet<string> dependencies = ordered
            .Select(contributor => contributor.Descriptor.PhasePlans.Single(
                plan =>
                    plan.Phase ==
                        TenantTerminationContributionPhase.Destroy))
            .SelectMany(plan => plan.DependsOnOwnerKeys)
            .ToHashSet(StringComparer.Ordinal);
        ITenantTerminationContributor[] terminalOwners = ordered
            .Where(contributor =>
                !dependencies.Contains(contributor.Descriptor.OwnerKey))
            .ToArray();
        if (terminalOwners.Length != 1)
        {
            return Invalid();
        }

        string ownerProofSetSha256;
        try
        {
            ownerProofSetSha256 = TenantTerminationVerificationProofSet
                .ComputeSha256(process, workItems);
        }
        catch (InvalidOperationException)
        {
            return Invalid();
        }

        return Result.Success(new TenantTerminationVerificationPlan(
            destroyOperationRevision,
            process.OperationRevision,
            ownerProofSetSha256,
            terminalOwners[0].Descriptor.OwnerKey,
            ordered.Select(contributor =>
                byOwner[contributor.Descriptor.OwnerKey]).ToArray()));
    }

    private static bool Matches(
        TenantTerminationProcess process,
        long destroyOperationRevision,
        TenantTerminationContributorDescriptor descriptor,
        Dictionary<string, TenantTerminationOwnerWorkItem> byOwner)
    {
        if (!byOwner.TryGetValue(
                descriptor.OwnerKey,
                out TenantTerminationOwnerWorkItem? workItem))
        {
            return false;
        }

        Guid expectedId = TenantTerminationExecutionIdentity.CreateWorkItemId(
            process.Id,
            destroyOperationRevision,
            TenantTerminationOwnerPhase.Destroy,
            descriptor.OwnerKey);
        Guid expectedIdempotencyKey = TenantTerminationExecutionIdentity
            .CreateWorkItemIdempotencyKey(
                process.Id,
                destroyOperationRevision,
                TenantTerminationOwnerPhase.Destroy,
                descriptor.OwnerKey);
        return workItem.Id == expectedId &&
            string.Equals(
                workItem.ScopeId,
                process.ScopeId,
                StringComparison.Ordinal) &&
            workItem.Matches(
                process.Id,
                TenantTerminationOwnerPhase.Destroy,
                descriptor.OwnerKey,
                destroyOperationRevision,
                expectedIdempotencyKey) &&
            workItem.CaseId == process.CaseId &&
            workItem.ApprovalRevision == process.ApprovalRevision &&
            workItem.TerminationEpoch == process.TerminationEpoch &&
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
            workItem.State == TenantTerminationOwnerWorkState.Completed;
    }

    private static Result<TenantTerminationVerificationPlan> Invalid() =>
        Result.Failure<TenantTerminationVerificationPlan>(
            DataRightsApplicationErrors
                .TenantTerminationVerificationProofInvalid);
}

internal sealed record TenantTerminationVerificationPlan(
    long DestroyOperationRevision,
    long VerificationOperationRevision,
    string OwnerProofSetSha256,
    string TerminalOwnerKey,
    IReadOnlyList<TenantTerminationOwnerWorkItem> WorkItems);
