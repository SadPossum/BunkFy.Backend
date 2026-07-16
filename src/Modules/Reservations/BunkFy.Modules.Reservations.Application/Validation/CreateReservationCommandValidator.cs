namespace BunkFy.Modules.Reservations.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class CreateReservationCommandValidator : ICommandValidator<CreateReservationCommand>
{
    public IEnumerable<string> Validate(CreateReservationCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (command.Arrival >= command.Departure)
        {
            yield return "Arrival must be before Departure.";
        }

        if (!HasMinutePrecision(command.ExpectedArrivalTime) || !HasMinutePrecision(command.ExpectedDepartureTime))
        {
            yield return "Expected arrival and departure times must use minute precision.";
        }

        if (command.InventoryUnitIds is null ||
            command.InventoryUnitIds.Count is 0 or > ReservationsContractLimits.MaximumRequestedUnits ||
            command.InventoryUnitIds.Any(id => id == Guid.Empty) ||
            command.InventoryUnitIds.Distinct().Count() != command.InventoryUnitIds.Count)
        {
            yield return "InventoryUnitIds must contain unique, non-empty ids within the supported limit.";
        }

        if (string.IsNullOrWhiteSpace(command.PrimaryGuestName) ||
            command.PrimaryGuestName.Trim().Length > ReservationsContractLimits.PrimaryGuestNameMaxLength)
        {
            yield return "PrimaryGuestName is required and is too long.";
        }

        if (command.GuestCount <= 0)
        {
            yield return "GuestCount must be greater than zero.";
        }

        if (command.SourceKind is not (ReservationSourceKind.Direct or ReservationSourceKind.External))
        {
            yield return "SourceKind must be Direct or External.";
        }
    }

    private static bool HasMinutePrecision(TimeOnly? value) =>
        !value.HasValue || value.Value.Ticks % TimeSpan.TicksPerMinute == 0;
}
