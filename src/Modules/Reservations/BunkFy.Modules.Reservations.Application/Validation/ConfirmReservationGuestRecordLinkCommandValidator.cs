namespace BunkFy.Modules.Reservations.Application.Validation;

using BunkFy.Modules.Reservations.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class ConfirmReservationGuestRecordLinkCommandValidator
    : ICommandValidator<ConfirmReservationGuestRecordLinkCommand>
{
    public IEnumerable<string> Validate(
        ConfirmReservationGuestRecordLinkCommand command)
    {
        foreach (string error in ReservationGuestRecordLinkValidation.Coordinates(
            command.OperationId,
            command.PropertyId,
            command.ReservationId))
        {
            yield return error;
        }

        if (command.CreationConfirmationId == Guid.Empty)
        {
            yield return "CreationConfirmationId is required.";
        }
    }
}
