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
    ReservationMutationCoordinator mutations,
    IInventoryProjectionRepository inventoryProjection,
    IReservationManagementOperationRepository operations,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ReassignReservationInventoryCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        ReassignReservationInventoryCommand command,
        CancellationToken cancellationToken)
    {
        if (command.AmendmentRequestId == Guid.Empty)
        {
            return Result.Failure<ReservationMutationReceiptDto>(
                ReservationsApplicationErrors.ManagementOperationInvalid);
        }

        Reservation? reservation = await mutations.AcquireOperationalAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        string actorId = command.ActorId?.Trim() ?? string.Empty;
        if (actorId.Length is 0 or > Reservation.ActorIdMaxLength)
        {
            return Result.Failure<ReservationMutationReceiptDto>(
                ReservationsApplicationErrors.DetailsChangeProvenanceInvalid);
        }

        string fingerprint = Fingerprint(command);
        ReservationManagementOperationRecord? existing = await operations
            .GetAsync(command.ReservationId, command.AmendmentRequestId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesInventoryAmendment(
                    command.ExpectedDetailsRevision,
                    fingerprint)
                ? Result.Success(reservation.ToMutationReceipt())
                : Result.Failure<ReservationMutationReceiptDto>(
                    ReservationsApplicationErrors.ManagementOperationConflict);
        }

        if (reservation.PendingAllocationAmendmentId == command.AmendmentRequestId &&
            string.Equals(
                reservation.PendingAllocationAmendmentRequestFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            if (reservation.PendingDetailsChangeOrigin !=
                ReservationDetailsChangeOrigin.Staff)
            {
                return Result.Failure<ReservationMutationReceiptDto>(
                    ReservationsApplicationErrors.AllocationAmendmentInProgress);
            }

            await operations.AddAsync(
                CreateOperation(reservation, command, fingerprint, clock.UtcNow),
                cancellationToken).ConfigureAwait(false);
            return Result.Success(reservation.ToMutationReceipt());
        }

        InventoryUnitSelectionValidation selection = await inventoryProjection.ValidateSelectionAsync(
            command.PropertyId,
            command.InventoryUnitIds,
            cancellationToken).ConfigureAwait(false);
        if (selection != InventoryUnitSelectionValidation.Valid)
        {
            return Result.Failure<ReservationMutationReceiptDto>(
                selection == InventoryUnitSelectionValidation.UnitNotFound
                    ? ReservationsApplicationErrors.InventoryUnitNotFound
                    : ReservationsApplicationErrors.InventoryUnitPropertyMismatch);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
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
            actorId,
            adapterConnectionId: null,
            externalOperationId: null,
            command.AmendmentRequestId,
            idGenerator.NewId(),
            nowUtc,
            reservation.ExpectedArrivalTime,
            reservation.ExpectedDepartureTime);
        if (begun.IsFailure)
        {
            return Result.Failure<ReservationMutationReceiptDto>(begun.Error);
        }

        if (begun.Value == ReservationDetailsChangeOutcome.Changed)
        {
            await operations.AddAsync(
                CreateOperation(reservation, command, fingerprint, nowUtc),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(reservation.ToMutationReceipt());
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

    private static ReservationManagementOperationRecord CreateOperation(
        Reservation reservation,
        ReassignReservationInventoryCommand command,
        string fingerprint,
        DateTimeOffset createdAtUtc) => new(
            command.AmendmentRequestId,
            reservation.ScopeId,
            command.PropertyId,
            command.ReservationId,
            ReservationManagementOperationKind.InventoryAmendment,
            ExpectedVersion: null,
            command.ExpectedDetailsRevision,
            BusinessDate: null,
            createdAtUtc,
            fingerprint);
}
