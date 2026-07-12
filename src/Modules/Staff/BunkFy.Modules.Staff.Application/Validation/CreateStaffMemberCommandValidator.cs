namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Commands;

internal sealed class CreateStaffMemberCommandValidator : ICommandValidator<CreateStaffMemberCommand>
{
    public IEnumerable<string> Validate(CreateStaffMemberCommand command) => StaffValidation.Profile(
        command.DisplayName, command.LegalName, command.WorkEmail, command.WorkPhone,
        command.EmployeeNumber, command.JobTitle, command.Department, command.AuthSubjectId, null, command.ActorId);
}
