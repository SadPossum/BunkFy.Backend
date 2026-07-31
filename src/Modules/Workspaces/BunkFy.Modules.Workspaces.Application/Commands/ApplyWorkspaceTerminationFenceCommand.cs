namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyWorkspaceTerminationFenceCommand(
    Guid IdempotencyKey,
    Guid ProcessId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid WorkItemId,
    Guid TerminationEpoch,
    string PolicyEvidenceSha256,
    string ActorId)
    : ITransactionalCommand<WorkspaceTerminationFenceReceiptDto>,
      IWorkspacePersistenceRetryableCommand;
