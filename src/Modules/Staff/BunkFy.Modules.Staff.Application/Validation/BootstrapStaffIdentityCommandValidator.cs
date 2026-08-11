namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class BootstrapStaffIdentityCommandValidator
    : ICommandValidator<BootstrapStaffIdentityCommand>
{
    public IEnumerable<string> Validate(BootstrapStaffIdentityCommand command)
    {
        if (command.OperationId == Guid.Empty)
        {
            yield return "OperationId is required.";
        }

        foreach (string error in StaffValidation.Profile(
            command.DisplayName,
            legalName: null,
            email: command.WorkEmail,
            phone: null,
            employeeNumber: null,
            jobTitle: null,
            department: null,
            command.AuthSubjectId,
            version: null,
            command.ActorId))
        {
            yield return error;
        }
    }
}
