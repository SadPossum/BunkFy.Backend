namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;

internal sealed record BeginTenantTerminationPhaseCommand(
    Guid ProcessId,
    TenantTerminationProcessPhase Phase,
    long ExpectedProcessVersion,
    string ActorId)
    : ITransactionalCommand<TenantTerminationPhaseStart>;

internal sealed record TenantTerminationPhaseStart(
    Guid ProcessId,
    TenantTerminationProcessPhase Phase,
    long ProcessVersion,
    long OperationRevision,
    IReadOnlyList<TenantTerminationPlannedDispatch> ReadyDispatches);
