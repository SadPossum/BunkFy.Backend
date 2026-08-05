namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;

internal sealed record RecordTenantTerminationOwnerResultCommand(
    Guid ProcessId,
    Guid WorkItemId,
    long OperationRevision,
    TenantTerminationContributionPhase Phase,
    string OwnerKey,
    Guid TaskRunId,
    int TaskAttempt,
    long ExpectedWorkItemVersion)
    : ITransactionalCommand<TenantTerminationOwnerResultRecorded>;

internal sealed record TenantTerminationOwnerResultRecorded(
    TenantTerminationOwnerWorkState State,
    long WorkItemVersion,
    IReadOnlyList<TenantTerminationPlannedDispatch> ReadyDispatches);
