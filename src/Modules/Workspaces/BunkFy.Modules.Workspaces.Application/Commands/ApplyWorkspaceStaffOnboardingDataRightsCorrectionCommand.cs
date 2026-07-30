namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyWorkspaceStaffOnboardingDataRightsCorrectionCommand(
    Guid ExecutionId,
    Guid CaseId,
    long ApprovalRevision,
    Guid ApplicationId,
    long ExpectedVersion,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department,
    string ActorId)
    : ITransactionalCommand<
        WorkspaceStaffOnboardingDataRightsCorrectionReceiptDto>;
