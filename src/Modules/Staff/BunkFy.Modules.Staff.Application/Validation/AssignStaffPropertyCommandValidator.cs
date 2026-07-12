namespace BunkFy.Modules.Staff.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;

internal sealed class AssignStaffPropertyCommandValidator : ICommandValidator<AssignStaffPropertyCommand>
{
    public IEnumerable<string> Validate(AssignStaffPropertyCommand command)
    {
        if (command.StaffMemberId == Guid.Empty || command.PropertyId == Guid.Empty)
        {
            yield return "StaffMemberId and PropertyId are required.";
        }

        if (command.PropertyJobTitle?.Trim().Length > StaffContractLimits.JobTitleMaxLength)
        {
            yield return "PropertyJobTitle exceeds the supported limit.";
        }

        if (command.EffectiveFrom == default)
        {
            yield return "EffectiveFrom is required.";
        }

        foreach (string error in StaffValidation.Common(command.ExpectedVersion, command.ActorId))
        {
            yield return error;
        }
    }
}
