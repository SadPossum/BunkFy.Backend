namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;

internal sealed class SetStaffAuthSubjectCommandValidator : ICommandValidator<SetStaffAuthSubjectCommand>
{
    public IEnumerable<string> Validate(SetStaffAuthSubjectCommand command)
    {
        if (command.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }

        if (command.AuthSubjectId?.Trim().Length > StaffContractLimits.AuthSubjectIdMaxLength)
        {
            yield return "AuthSubjectId exceeds the supported limit.";
        }

        foreach (string error in StaffValidation.Common(command.ExpectedVersion, command.ActorId))
        {
            yield return error;
        }
    }
}
