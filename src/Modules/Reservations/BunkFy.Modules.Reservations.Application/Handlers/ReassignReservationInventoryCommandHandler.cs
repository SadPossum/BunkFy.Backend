namespace BunkFy.Modules.Reservations.Application.Handlers;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class ReassignReservationInventoryCommandHandler(
    IReservationRepository reservations,
    IInventoryProjectionRepository inventoryProjection,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ReassignReservationInventoryCommand, ReservationDto>
{
    public async Task<Result<ReservationDto>> HandleAsync(
        ReassignReservationInventoryCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations.GetAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        InventoryUnitSelectionValidation selection = await inventoryProjection.ValidateSelectionAsync(
            command.PropertyId,
            command.InventoryUnitIds,
            cancellationToken).ConfigureAwait(false);
        if (selection != InventoryUnitSelectionValidation.Valid)
        {
            return Result.Failure<ReservationDto>(
                selection == InventoryUnitSelectionValidation.UnitNotFound
                    ? ReservationsApplicationErrors.InventoryUnitNotFound
                    : ReservationsApplicationErrors.InventoryUnitPropertyMismatch);
        }

        string fingerprint = Fingerprint(command);
        Result<ReservationDetailsChangeOutcome> begun = reservation.BeginAllocationAmendment(
            command.AmendmentRequestId,
            fingerprint,
            reservation.Arrival,
            reservation.Departure,
            command.InventoryUnitIds,
            reservation.PrimaryGuestName,
            reservation.Email,
            reservation.Phone,
            reservation.GuestCount,
            reservation.Notes,
            command.ExpectedDetailsRevision,
            ReservationDetailsChangeOrigin.Staff,
            command.ActorId,
            adapterConnectionId: null,
            externalOperationId: null,
            command.AmendmentRequestId,
            idGenerator.NewId(),
            clock.UtcNow,
            reservation.ExpectedArrivalTime,
            reservation.ExpectedDepartureTime);
        return begun.IsFailure
            ? Result.Failure<ReservationDto>(begun.Error)
            : Result.Success(reservation.ToDto());
    }

    private static string Fingerprint(ReassignReservationInventoryCommand command)
    {
        string canonical = string.Join(
            '|',
            command.ReservationId.ToString("N"),
            command.AmendmentRequestId.ToString("N"),
            command.ExpectedDetailsRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string.Join(',', command.InventoryUnitIds.Order().Select(id => id.ToString("N"))));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
