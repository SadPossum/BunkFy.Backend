namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Commands;

internal sealed class UnassignStaffPropertyCommandValidator : ICommandValidator<UnassignStaffPropertyCommand>
{
    public IEnumerable<string> Validate(UnassignStaffPropertyCommand command)
    {
        if (command.StaffMemberId == Guid.Empty || command.PropertyId == Guid.Empty)
        {
            yield return "StaffMemberId and PropertyId are required.";
        }

        if (command.EffectiveTo == default)
        {
            yield return "EffectiveTo is required.";
        }

        foreach (string error in StaffValidation.Reason(command.Reason))
        {
            yield return error;
        }

        foreach (string error in StaffValidation.Common(command.ExpectedVersion, command.ActorId))
        {
            yield return error;
        }
    }
}
