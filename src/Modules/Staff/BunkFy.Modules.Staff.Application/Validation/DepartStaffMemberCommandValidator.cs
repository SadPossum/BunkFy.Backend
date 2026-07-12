namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Commands;

internal sealed class DepartStaffMemberCommandValidator : ICommandValidator<DepartStaffMemberCommand>
{
    public IEnumerable<string> Validate(DepartStaffMemberCommand command)
    {
        if (command.EffectiveOn == default)
        {
            yield return "EffectiveOn is required.";
        }

        foreach (string error in StaffValidation.Lifecycle(command.StaffMemberId,
            command.Reason, command.ExpectedVersion, command.ActorId))
        {
            yield return error;
        }
    }
}
