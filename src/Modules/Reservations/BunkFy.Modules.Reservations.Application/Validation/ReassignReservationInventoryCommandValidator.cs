namespace BunkFy.Modules.Reservations.Application.Validation;

using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class ReassignReservationInventoryCommandValidator
    : ICommandValidator<ReassignReservationInventoryCommand>
{
    public IEnumerable<string> Validate(ReassignReservationInventoryCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (command.ReservationId == Guid.Empty)
        {
            yield return "ReservationId is required.";
        }

        if (command.AmendmentRequestId == Guid.Empty)
        {
            yield return "AmendmentRequestId is required.";
        }

        if (command.InventoryUnitIds is null ||
            command.InventoryUnitIds.Count is 0 or > Reservation.MaximumRequestedUnits ||
            command.InventoryUnitIds.Any(id => id == Guid.Empty) ||
            command.InventoryUnitIds.Distinct().Count() != command.InventoryUnitIds.Count)
        {
            yield return "InventoryUnitIds must contain unique, non-empty ids within the supported limit.";
        }

        if (command.ExpectedDetailsRevision <= 0)
        {
            yield return "ExpectedDetailsRevision must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(command.ActorId) || command.ActorId.Trim().Length > Reservation.ActorIdMaxLength)
        {
            yield return "ActorId is required and is too long.";
        }
    }
}
