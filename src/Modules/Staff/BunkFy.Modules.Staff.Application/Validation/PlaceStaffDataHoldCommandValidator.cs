namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

internal sealed class PlaceStaffDataHoldCommandValidator
    : ICommandValidator<PlaceStaffDataHoldCommand>
{
    public IEnumerable<string> Validate(
        PlaceStaffDataHoldCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty)
        {
            yield return "IdempotencyKey is required.";
        }

        if (command.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }

        if (command.ExpectedStaffVersion < 1)
        {
            yield return "ExpectedStaffVersion must be positive.";
        }

        string reasonCode =
            command.ReasonCode?.Trim().ToLowerInvariant() ??
            string.Empty;
        if (!StaffDataHoldReasonCodes.All.Contains(reasonCode))
        {
            yield return "ReasonCode is not supported.";
        }

        foreach (string error in StaffValidation.Common(
                     version: null,
                     command.ActorId))
        {
            yield return error;
        }
    }
}
