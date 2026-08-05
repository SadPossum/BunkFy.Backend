namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed record PrepareTenantTerminationVerificationCommand(
    Guid ProcessId,
    long VerificationOperationRevision,
    Guid TaskRunId,
    int TaskAttempt)
    : ITransactionalCommand<TenantTerminationVerificationStart>;

internal sealed record TenantTerminationVerificationStart(
    bool DispatchRequired,
    long ProcessVersion,
    long DestroyOperationRevision,
    long VerificationOperationRevision,
    string OwnerProofSetSha256,
    string TerminalOwnerKey,
    IReadOnlyList<TenantTerminationOwnerWorkItem> WorkItems);
