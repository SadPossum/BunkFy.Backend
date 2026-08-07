namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Commands;

internal sealed class CreateStaffMemberCommandValidator : ICommandValidator<CreateStaffMemberCommand>
{
    public IEnumerable<string> Validate(CreateStaffMemberCommand command)
    {
        if (command.OperationId == Guid.Empty)
        {
            yield return "OperationId is required.";
        }

        foreach (string error in StaffValidation.Profile(
                     command.DisplayName,
                     command.LegalName,
                     command.WorkEmail,
                     command.WorkPhone,
                     command.EmployeeNumber,
                     command.JobTitle,
                     command.Department,
                     command.AuthSubjectId,
                     version: null,
                     command.ActorId))
        {
            yield return error;
        }
    }
}
