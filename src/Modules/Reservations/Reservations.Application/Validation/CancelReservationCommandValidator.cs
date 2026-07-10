namespace Reservations.Application.Validation;

using Gma.Framework.Cqrs;
using Reservations.Application.Commands;

internal sealed class CancelReservationCommandValidator : ICommandValidator<CancelReservationCommand>
{
    public IEnumerable<string> Validate(CancelReservationCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (command.ReservationId == Guid.Empty)
        {
            yield return "ReservationId is required.";
        }

        if (command.ExpectedVersion <= 0)
        {
            yield return "ExpectedVersion must be greater than zero.";
        }
    }
}
