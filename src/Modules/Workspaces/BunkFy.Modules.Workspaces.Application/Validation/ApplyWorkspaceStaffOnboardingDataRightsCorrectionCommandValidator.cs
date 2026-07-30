namespace BunkFy.Modules.Workspaces.Application.Validation;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;

internal sealed class
    ApplyWorkspaceStaffOnboardingDataRightsCorrectionCommandValidator
    : ICommandValidator<
        ApplyWorkspaceStaffOnboardingDataRightsCorrectionCommand>
{
    public IEnumerable<string> Validate(
        ApplyWorkspaceStaffOnboardingDataRightsCorrectionCommand command)
    {
        if (command.ExecutionId == Guid.Empty)
        {
            yield return "ExecutionId is required.";
        }

        if (command.CaseId == Guid.Empty)
        {
            yield return "CaseId is required.";
        }

        if (command.ApprovalRevision < 1)
        {
            yield return "ApprovalRevision must be positive.";
        }

        if (command.ApplicationId == Guid.Empty)
        {
            yield return "ApplicationId is required.";
        }

        if (command.ExpectedVersion < 1)
        {
            yield return "ExpectedVersion must be positive.";
        }

        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            yield return "ActorId is required.";
        }

        if (WorkspaceStaffApplicantProfile.Create(
                command.DisplayName,
                command.LegalName,
                command.WorkEmail,
                command.WorkPhone,
                command.EmployeeNumber,
                command.JobTitle,
                command.Department).IsFailure)
        {
            yield return "The applicant profile is invalid.";
        }
    }
}
