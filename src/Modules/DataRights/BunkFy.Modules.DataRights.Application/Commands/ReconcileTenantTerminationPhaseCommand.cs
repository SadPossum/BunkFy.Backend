namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;

internal sealed record ReconcileTenantTerminationPhaseCommand(
    Guid ProcessId,
    TenantTerminationProcessPhase Phase,
    long OperationRevision,
    long ExpectedProcessVersion,
    string ActorId)
    : ITransactionalCommand<TenantTerminationPhaseReconciliation>;

internal sealed record TenantTerminationPhaseReconciliation(
    Guid ProcessId,
    TenantTerminationProcessPhase Phase,
    TenantTerminationProcessStatus Status,
    long ProcessVersion,
    long OperationRevision,
    IReadOnlyList<TenantTerminationPlannedDispatch> ReadyDispatches,
    bool ExportArtifactRequired);
