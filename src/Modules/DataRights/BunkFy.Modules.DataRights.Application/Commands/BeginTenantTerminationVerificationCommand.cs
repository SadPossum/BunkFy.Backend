namespace BunkFy.Modules.DataRights.Application.Commands;

using Gma.Framework.Cqrs;

internal sealed record BeginTenantTerminationVerificationCommand(
    Guid ProcessId,
    long ExpectedProcessVersion,
    string ActorId)
    : ITransactionalCommand<TenantTerminationVerificationPhaseStart>;

internal sealed record TenantTerminationVerificationPhaseStart(
    Guid ProcessId,
    long ProcessVersion,
    long DestroyOperationRevision,
    long VerificationOperationRevision,
    Guid TaskRunId);
