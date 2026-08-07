namespace BunkFy.Modules.Reservations.Application.Validation;

using BunkFy.Modules.Reservations.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RetryReservationGuestRecordLinkCommandValidator
    : ICommandValidator<RetryReservationGuestRecordLinkCommand>
{
    public IEnumerable<string> Validate(
        RetryReservationGuestRecordLinkCommand command) =>
        ReservationGuestRecordLinkValidation.Coordinates(
            command.OperationId,
            command.PropertyId,
            command.ReservationId);
}
