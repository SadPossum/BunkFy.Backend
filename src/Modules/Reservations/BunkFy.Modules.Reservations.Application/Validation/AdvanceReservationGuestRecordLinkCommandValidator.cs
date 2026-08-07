namespace BunkFy.Modules.Reservations.Application.Validation;

using BunkFy.Modules.Reservations.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class AdvanceReservationGuestRecordLinkCommandValidator
    : ICommandValidator<AdvanceReservationGuestRecordLinkCommand>
{
    public IEnumerable<string> Validate(
        AdvanceReservationGuestRecordLinkCommand command)
    {
        foreach (string error in ReservationGuestRecordLinkValidation.Coordinates(
            command.OperationId,
            command.PropertyId,
            command.ReservationId))
        {
            yield return error;
        }

        if (command.DispatchRevision <= 0)
        {
            yield return "DispatchRevision must be greater than zero.";
        }
    }
}
