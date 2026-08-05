namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Ports;
using Gma.Framework.Cqrs;

internal sealed record CompleteTenantTerminationVerificationCommand(
    Guid ProcessId,
    long DestroyOperationRevision,
    long VerificationOperationRevision,
    Guid TaskRunId,
    int TaskAttempt,
    long ExpectedProcessVersion,
    string ExpectedOwnerProofSetSha256,
    string ExpectedTerminalOwnerKey,
    TenantTerminationReplayCheckpoint ReplayCheckpoint)
    : ITransactionalCommand<TenantTerminationVerificationCompleted>;

internal sealed record TenantTerminationVerificationCompleted(
    Guid ProcessId,
    long ProcessVersion,
    Guid TerminalReceiptId,
    long TerminalReceiptVersion);
