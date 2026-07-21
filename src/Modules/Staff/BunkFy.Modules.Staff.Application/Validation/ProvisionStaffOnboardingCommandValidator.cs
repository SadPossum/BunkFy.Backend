namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class ProvisionStaffOnboardingCommandValidator
    : ICommandValidator<ProvisionStaffOnboardingCommand>
{
    public IEnumerable<string> Validate(ProvisionStaffOnboardingCommand command)
    {
        foreach (string error in StaffValidation.Profile(
            command.DisplayName,
            command.LegalName,
            command.WorkEmail,
            command.WorkPhone,
            command.EmployeeNumber,
            command.JobTitle,
            command.Department,
            command.AuthSubjectId,
            null,
            command.ActorId))
        {
            yield return error;
        }

        foreach (string error in StaffValidation.Reason(command.Reason))
        {
            yield return error;
        }
    }
}
