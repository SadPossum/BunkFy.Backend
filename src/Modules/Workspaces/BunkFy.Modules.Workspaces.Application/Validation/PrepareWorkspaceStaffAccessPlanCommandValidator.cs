namespace BunkFy.Modules.Workspaces.Application.Validation;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;

internal sealed class PrepareWorkspaceStaffAccessPlanCommandValidator
    : ICommandValidator<PrepareWorkspaceStaffAccessPlanCommand>
{
    public IEnumerable<string> Validate(PrepareWorkspaceStaffAccessPlanCommand command)
    {
        if (command.SourceId == Guid.Empty ||
            command.SourceKind is not (WorkspaceStaffOnboardingSourceKind.Invitation or
                WorkspaceStaffOnboardingSourceKind.EnrollmentLink))
        {
            yield return "A valid join source is required.";
        }

        if (string.IsNullOrWhiteSpace(command.ProfileKey) ||
            command.ProfileKey.Trim().Length > WorkspaceStaffAccessPlan.ProfileKeyMaxLength)
        {
            yield return "ProfileKey is required and must be within the supported limit.";
        }

        if (command.PropertyIds is null ||
            command.PropertyIds.Count > WorkspaceStaffAccessPlan.PropertyCountMax ||
            command.PropertyIds.Any(propertyId => propertyId == Guid.Empty))
        {
            yield return "PropertyIds must contain no more than the supported number of valid identifiers.";
        }

        if (string.IsNullOrWhiteSpace(command.ActorSubjectId) ||
            command.ActorSubjectId.Trim().Length > WorkspaceStaffAccessPlan.SubjectIdMaxLength)
        {
            yield return "ActorSubjectId is required and must be within the supported limit.";
        }
    }
}

internal sealed class ActivateWorkspaceStaffAccessPlanCommandValidator
    : ICommandValidator<ActivateWorkspaceStaffAccessPlanCommand>
{
    public IEnumerable<string> Validate(ActivateWorkspaceStaffAccessPlanCommand command)
    {
        if (command.SourceId == Guid.Empty)
        {
            yield return "SourceId is required.";
        }
    }
}
