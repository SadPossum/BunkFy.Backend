namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

public sealed record SubmitWorkspaceStaffOnboardingCommand(
    WorkspaceStaffOnboardingSourceKind SourceKind,
    string Token,
    string SubjectId,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department) : ITransactionalCommand<WorkspaceStaffOnboardingDto>;
