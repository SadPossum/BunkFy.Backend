namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyWorkspaceStaffOnboardingProcessingRestrictionCommand(
    Guid IdempotencyKey,
    Guid CaseId,
    long ApprovalRevision,
    Guid ApplicationId,
    long ExpectedOnboardingVersion,
    long ExpectedProjectionRevision,
    string ActorId)
    : ITransactionalCommand<
        WorkspaceStaffOnboardingProcessingRestrictionReceiptDto>;
