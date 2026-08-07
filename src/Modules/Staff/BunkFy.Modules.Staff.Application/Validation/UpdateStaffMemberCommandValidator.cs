namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Commands;

internal sealed class UpdateStaffMemberCommandValidator : ICommandValidator<UpdateStaffMemberCommand>
{
    public IEnumerable<string> Validate(UpdateStaffMemberCommand command)
    {
        if (command.OperationId == Guid.Empty)
        {
            yield return "OperationId is required.";
        }

        if (command.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }

        foreach (string error in StaffValidation.Profile(command.DisplayName, command.LegalName,
            command.WorkEmail, command.WorkPhone, command.EmployeeNumber, command.JobTitle,
            command.Department, null, command.ExpectedVersion, command.ActorId))
        {
            yield return error;
        }
    }
}
