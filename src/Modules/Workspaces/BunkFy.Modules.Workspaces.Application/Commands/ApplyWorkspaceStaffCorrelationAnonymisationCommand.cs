namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

internal sealed record ApplyWorkspaceStaffCorrelationAnonymisationCommand(
    Guid IdempotencyKey,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid AnchorProcessId,
    long ExpectedAnchorVersion,
    DataRightsApprovalEvidence ApprovalEvidence,
    string ActorId)
    : ITransactionalCommand<
        WorkspaceStaffCorrelationAnonymisationReceiptDto>;
