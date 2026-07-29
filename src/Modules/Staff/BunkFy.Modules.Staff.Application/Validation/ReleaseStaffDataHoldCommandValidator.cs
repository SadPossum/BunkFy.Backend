namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class ReleaseStaffDataHoldCommandValidator
    : ICommandValidator<ReleaseStaffDataHoldCommand>
{
    public IEnumerable<string> Validate(
        ReleaseStaffDataHoldCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty)
        {
            yield return "IdempotencyKey is required.";
        }

        if (command.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }

        if (command.HoldId == Guid.Empty)
        {
            yield return "HoldId is required.";
        }

        if (command.ExpectedStaffVersion < 1)
        {
            yield return "ExpectedStaffVersion must be positive.";
        }

        if (command.ExpectedHoldVersion < 1)
        {
            yield return "ExpectedHoldVersion must be positive.";
        }

        foreach (string error in StaffValidation.Common(
                     version: null,
                     command.ActorId))
        {
            yield return error;
        }
    }
}
