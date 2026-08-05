namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

internal sealed class TenantTerminationPhasePlanner
{
    private readonly ITenantTerminationContributor[] contributors;

    public TenantTerminationPhasePlanner(
        IEnumerable<ITenantTerminationContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        this.contributors = contributors.ToArray();
    }

    public Result<IReadOnlyList<TenantTerminationPlannedOwnerWork>> Plan(
        TenantTerminationProcess process) =>
        this.Plan(process, requireRunning: true);

    private Result<IReadOnlyList<TenantTerminationPlannedOwnerWork>> Plan(
        TenantTerminationProcess process,
        bool requireRunning)
    {
        ArgumentNullException.ThrowIfNull(process);

        if ((requireRunning &&
                process.Status != TenantTerminationProcessStatus.Running) ||
            process.OperationRevision <= process.ApprovalRevision ||
            !TryMapPhase(
                process.Phase,
                out TenantTerminationContributionPhase contributionPhase,
                out TenantTerminationOwnerPhase ownerPhase))
        {
            return Invalid<IReadOnlyList<TenantTerminationPlannedOwnerWork>>();
        }

        Result<IReadOnlyList<ITenantTerminationContributor>> ordered =
            TenantTerminationContributorSet.OrderForPhase(
                this.contributors,
                contributionPhase);
        if (ordered.IsFailure)
        {
            return Result.Failure<
                IReadOnlyList<TenantTerminationPlannedOwnerWork>>(
                    ordered.Error);
        }

        TenantTerminationPlannedOwnerWork[] planned = ordered.Value
            .Select(contributor => CreatePlan(
                process,
                contributor.Descriptor,
                contributionPhase,
                ownerPhase))
            .ToArray();
        return Result.Success<
            IReadOnlyList<TenantTerminationPlannedOwnerWork>>(planned);
    }

    public Result<IReadOnlyList<TenantTerminationOwnerWorkItem>>
        PrepareWorkItems(
            TenantTerminationProcess process,
            DateTimeOffset nowUtc)
    {
        Result<IReadOnlyList<TenantTerminationPlannedOwnerWork>> planned =
            this.Plan(process);
        if (planned.IsFailure)
        {
            return Result.Failure<
                IReadOnlyList<TenantTerminationOwnerWorkItem>>(planned.Error);
        }

        List<TenantTerminationOwnerWorkItem> workItems = [];
        foreach (TenantTerminationPlannedOwnerWork owner in planned.Value)
        {
            Result<TenantTerminationOwnerWorkItem> prepared =
                TenantTerminationOwnerWorkItem.Prepare(
                    owner.WorkItemId,
                    process.ScopeId,
                    process.Id,
                    process.CaseId,
                    process.ApprovalRevision,
                    process.OperationRevision,
                    process.TerminationEpoch,
                    owner.IdempotencyKey,
                    owner.Phase,
                    owner.OwnerKey,
                    owner.OwnerContractVersion,
                    owner.CatalogVersion,
                    owner.CatalogSha256,
                    process.PolicyEvidenceSha256,
                    nowUtc);
            if (prepared.IsFailure)
            {
                return Result.Failure<
                    IReadOnlyList<TenantTerminationOwnerWorkItem>>(
                        prepared.Error);
            }

            workItems.Add(prepared.Value);
        }

        return Result.Success<
            IReadOnlyList<TenantTerminationOwnerWorkItem>>(workItems);
    }

    public Result<IReadOnlyList<TenantTerminationPlannedDispatch>>
        FindReadyDispatches(
            TenantTerminationProcess process,
            IReadOnlyCollection<TenantTerminationOwnerWorkItem> workItems)
    {
        Result<TenantTerminationValidatedPhase> validated =
            this.ValidateWorkItems(process, workItems);
        if (validated.IsFailure)
        {
            return Result.Failure<
                IReadOnlyList<TenantTerminationPlannedDispatch>>(
                    validated.Error);
        }

        IReadOnlyDictionary<string, TenantTerminationOwnerWorkItem> byOwner =
            validated.Value.WorkByOwner;

        TenantTerminationContributionPhase contributionPhase =
            MapPhase(process.Phase);
        List<TenantTerminationPlannedDispatch> ready = [];
        foreach (TenantTerminationPlannedOwnerWork owner in
            validated.Value.PlannedWork)
        {
            TenantTerminationOwnerWorkItem workItem = byOwner[owner.OwnerKey];
            if (workItem.State is not TenantTerminationOwnerWorkState.Prepared and
                    not TenantTerminationOwnerWorkState.RetryRequired ||
                owner.DependsOnOwnerKeys.Any(dependency =>
                    byOwner[dependency].State !=
                        TenantTerminationOwnerWorkState.Completed) ||
                workItem.AttemptCount == int.MaxValue)
            {
                continue;
            }

            int dispatchSequence = workItem.AttemptCount + 1;
            Guid taskRunId = TenantTerminationExecutionIdentity.CreateTaskRunId(
                workItem.Id,
                dispatchSequence);
            ready.Add(new(
                process.Id,
                workItem.Id,
                process.OperationRevision,
                contributionPhase,
                owner.OwnerKey,
                owner.ExecutionBoundary,
                dispatchSequence,
                taskRunId,
                TenantTerminationExecutionIdentity
                    .CreateTaskDeduplicationKey(taskRunId)));
        }

        return Result.Success<
            IReadOnlyList<TenantTerminationPlannedDispatch>>(ready);
    }

    public Result<TenantTerminationValidatedPhase> ValidateWorkItems(
        TenantTerminationProcess process,
        IReadOnlyCollection<TenantTerminationOwnerWorkItem> workItems) =>
        this.ValidateWorkItems(
            process,
            workItems,
            requireRunning: true);

    public Result<TenantTerminationValidatedPhase>
        ValidateRecoverableWorkItems(
            TenantTerminationProcess process,
            IReadOnlyCollection<TenantTerminationOwnerWorkItem> workItems)
    {
        if (process is null ||
            process.Status is not TenantTerminationProcessStatus.Blocked and
                not TenantTerminationProcessStatus.Failed)
        {
            return Invalid<TenantTerminationValidatedPhase>();
        }

        return this.ValidateWorkItems(
            process,
            workItems,
            requireRunning: false);
    }

    private Result<TenantTerminationValidatedPhase> ValidateWorkItems(
        TenantTerminationProcess process,
        IReadOnlyCollection<TenantTerminationOwnerWorkItem> workItems,
        bool requireRunning)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(workItems);

        Result<IReadOnlyList<TenantTerminationPlannedOwnerWork>> planned =
            this.Plan(process, requireRunning);
        if (planned.IsFailure)
        {
            return Result.Failure<TenantTerminationValidatedPhase>(
                planned.Error);
        }

        Dictionary<string, TenantTerminationOwnerWorkItem> byOwner =
            new(StringComparer.Ordinal);
        foreach (TenantTerminationOwnerWorkItem? workItem in workItems)
        {
            if (workItem is null ||
                !byOwner.TryAdd(workItem.OwnerKey, workItem))
            {
                return Invalid<TenantTerminationValidatedPhase>();
            }
        }

        if (byOwner.Count != planned.Value.Count ||
            planned.Value.Any(owner =>
                !byOwner.TryGetValue(
                    owner.OwnerKey,
                    out TenantTerminationOwnerWorkItem? workItem) ||
                !Matches(process, owner, workItem)))
        {
            return Invalid<TenantTerminationValidatedPhase>();
        }

        return Result.Success(new TenantTerminationValidatedPhase(
            planned.Value,
            byOwner));
    }

    private static TenantTerminationPlannedOwnerWork CreatePlan(
        TenantTerminationProcess process,
        TenantTerminationContributorDescriptor descriptor,
        TenantTerminationContributionPhase contributionPhase,
        TenantTerminationOwnerPhase ownerPhase)
    {
        TenantTerminationContributorPhasePlan phasePlan =
            descriptor.PhasePlans.Single(plan =>
                plan.Phase == contributionPhase);
        return new(
            descriptor.OwnerKey,
            ownerPhase,
            phasePlan.DependsOnOwnerKeys.ToArray(),
            phasePlan.ExecutionBoundary,
            descriptor.ContractVersion,
            descriptor.CatalogVersion,
            descriptor.CatalogSha256,
            TenantTerminationExecutionIdentity.CreateWorkItemId(
                process.Id,
                process.OperationRevision,
                ownerPhase,
                descriptor.OwnerKey),
            TenantTerminationExecutionIdentity.CreateWorkItemIdempotencyKey(
                process.Id,
                process.OperationRevision,
                ownerPhase,
                descriptor.OwnerKey));
    }

    private static bool Matches(
        TenantTerminationProcess process,
        TenantTerminationPlannedOwnerWork planned,
        TenantTerminationOwnerWorkItem workItem) =>
        workItem.Id == planned.WorkItemId &&
        string.Equals(
            workItem.ScopeId,
            process.ScopeId,
            StringComparison.Ordinal) &&
        workItem.Matches(
            process.Id,
            planned.Phase,
            planned.OwnerKey,
            process.OperationRevision,
            planned.IdempotencyKey) &&
        workItem.CaseId == process.CaseId &&
        workItem.ApprovalRevision == process.ApprovalRevision &&
        workItem.TerminationEpoch == process.TerminationEpoch &&
        workItem.OwnerContractVersion == planned.OwnerContractVersion &&
        workItem.CatalogVersion == planned.CatalogVersion &&
        string.Equals(
            workItem.CatalogSha256,
            planned.CatalogSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            workItem.PolicyEvidenceSha256,
            process.PolicyEvidenceSha256,
            StringComparison.Ordinal) &&
        Enum.IsDefined(workItem.State) &&
        workItem.State != TenantTerminationOwnerWorkState.Unknown;

    internal static bool TryMapPhase(
        TenantTerminationProcessPhase processPhase,
        out TenantTerminationContributionPhase contributionPhase,
        out TenantTerminationOwnerPhase ownerPhase)
    {
        contributionPhase = MapPhase(processPhase);
        ownerPhase = processPhase switch
        {
            TenantTerminationProcessPhase.Freeze =>
                TenantTerminationOwnerPhase.Freeze,
            TenantTerminationProcessPhase.Export =>
                TenantTerminationOwnerPhase.Export,
            TenantTerminationProcessPhase.Destroy =>
                TenantTerminationOwnerPhase.Destroy,
            TenantTerminationProcessPhase.Restore =>
                TenantTerminationOwnerPhase.Restore,
            _ => TenantTerminationOwnerPhase.Unknown
        };
        return contributionPhase != TenantTerminationContributionPhase.Unknown &&
            ownerPhase != TenantTerminationOwnerPhase.Unknown;
    }

    private static TenantTerminationContributionPhase MapPhase(
        TenantTerminationProcessPhase processPhase) =>
        processPhase switch
        {
            TenantTerminationProcessPhase.Freeze =>
                TenantTerminationContributionPhase.Freeze,
            TenantTerminationProcessPhase.Export =>
                TenantTerminationContributionPhase.Export,
            TenantTerminationProcessPhase.Destroy =>
                TenantTerminationContributionPhase.Destroy,
            TenantTerminationProcessPhase.Restore =>
                TenantTerminationContributionPhase.Restore,
            _ => TenantTerminationContributionPhase.Unknown
        };

    private static Result<T> Invalid<T>() =>
        Result.Failure<T>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}
