namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;

internal sealed record BeginTenantTerminationOwnerWorkCommand(
    Guid ProcessId,
    Guid WorkItemId,
    long OperationRevision,
    TenantTerminationContributionPhase Phase,
    string OwnerKey,
    TenantTerminationExecutionBoundary ExecutionBoundary,
    Guid TaskRunId,
    int TaskAttempt)
    : ITransactionalCommand<TenantTerminationOwnerWorkStart>;

internal sealed record TenantTerminationOwnerWorkStart(
    bool DispatchRequired,
    TenantTerminationOwnerWorkState State,
    long WorkItemVersion,
    string OwnerKey,
    int CatalogVersion,
    string CatalogSha256,
    TenantTerminationExecutionBoundary ExecutionBoundary,
    TenantTerminationContributionRequest? Request,
    IReadOnlyList<TenantTerminationPlannedDispatch> ReadyDispatches);
