namespace BunkFy.Modules.DataRights.Application.Models;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;

internal sealed record TenantTerminationPlannedOwnerWork(
    string OwnerKey,
    TenantTerminationOwnerPhase Phase,
    IReadOnlyCollection<string> DependsOnOwnerKeys,
    TenantTerminationExecutionBoundary ExecutionBoundary,
    int OwnerContractVersion,
    int CatalogVersion,
    string CatalogSha256,
    Guid WorkItemId,
    Guid IdempotencyKey);

internal sealed record TenantTerminationPlannedDispatch(
    Guid ProcessId,
    Guid WorkItemId,
    long OperationRevision,
    TenantTerminationContributionPhase Phase,
    string OwnerKey,
    TenantTerminationExecutionBoundary ExecutionBoundary,
    int DispatchSequence,
    Guid TaskRunId,
    string TaskDeduplicationKey);

internal sealed record TenantTerminationValidatedPhase(
    IReadOnlyList<TenantTerminationPlannedOwnerWork> PlannedWork,
    IReadOnlyDictionary<string, TenantTerminationOwnerWorkItem> WorkByOwner);

internal sealed record TenantTerminationPhaseEvaluation(
    TenantTerminationPhaseDisposition Disposition,
    string? OutcomeCode,
    DateTimeOffset? HoldReviewAtUtc,
    IReadOnlyList<TenantTerminationPlannedDispatch> ReadyDispatches);

internal enum TenantTerminationPhaseDisposition
{
    Unknown = 0,
    Running = 1,
    Completed = 2,
    Blocked = 3,
    Failed = 4
}
