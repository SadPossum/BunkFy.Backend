namespace BunkFy.Modules.Workspaces.Application.Validation;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;

internal sealed class SubmitWorkspaceStaffOnboardingCommandValidator
    : ICommandValidator<SubmitWorkspaceStaffOnboardingCommand>
{
    public IEnumerable<string> Validate(SubmitWorkspaceStaffOnboardingCommand command)
    {
        if (command.SourceKind is not (WorkspaceStaffOnboardingSourceKind.Invitation or
            WorkspaceStaffOnboardingSourceKind.EnrollmentLink))
        {
            yield return "SourceKind is invalid.";
        }

        if (string.IsNullOrWhiteSpace(command.Token) || command.Token.Length > 2048)
        {
            yield return "Token is invalid.";
        }

        if (string.IsNullOrWhiteSpace(command.SubjectId) ||
            command.SubjectId.Trim().Length > WorkspaceStaffOnboardingRules.SubjectIdMaxLength)
        {
            yield return "SubjectId is invalid.";
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName) ||
            command.DisplayName.Trim().Length > WorkspaceStaffOnboardingRules.DisplayNameMaxLength)
        {
            yield return "DisplayName is required and exceeds the supported limit.";
        }

        if (command.LegalName?.Trim().Length > WorkspaceStaffOnboardingRules.LegalNameMaxLength ||
            command.WorkEmail?.Trim().Length > WorkspaceStaffOnboardingRules.EmailMaxLength ||
            command.WorkPhone?.Trim().Length > WorkspaceStaffOnboardingRules.PhoneMaxLength ||
            command.EmployeeNumber?.Trim().Length > WorkspaceStaffOnboardingRules.EmployeeNumberMaxLength ||
            command.JobTitle?.Trim().Length > WorkspaceStaffOnboardingRules.JobTitleMaxLength ||
            command.Department?.Trim().Length > WorkspaceStaffOnboardingRules.DepartmentMaxLength)
        {
            yield return "One or more Staff profile fields exceed their supported limits.";
        }
    }
}
