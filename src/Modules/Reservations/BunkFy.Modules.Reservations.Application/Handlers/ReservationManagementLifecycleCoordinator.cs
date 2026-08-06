namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class ReservationManagementLifecycleCoordinator(
    ReservationMutationCoordinator mutations,
    IReservationManagementOperationRepository operations,
    ISystemClock clock)
{
    public async Task<Result<ReservationMutationReceiptDto>> ExecuteAsync(
        Guid operationId,
        Guid propertyId,
        Guid reservationId,
        ReservationManagementOperationKind kind,
        long expectedVersion,
        DateOnly? businessDate,
        string? actorId,
        Func<Reservation, DateTimeOffset, Result> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apply);
        if (!IsValidRequest(operationId, kind, businessDate, actorId))
        {
            return Result.Failure<ReservationMutationReceiptDto>(
                ReservationsApplicationErrors.ManagementOperationInvalid);
        }

        Reservation? reservation = await mutations.AcquireOperationalAsync(
            propertyId,
            reservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationMutationReceiptDto>(
                ReservationsApplicationErrors.ReservationNotFound);
        }

        ReservationManagementOperationRecord? existing = await operations
            .GetAsync(reservationId, operationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesLifecycle(kind, expectedVersion, businessDate)
                ? Result.Success(reservation.ToMutationReceipt())
                : Result.Failure<ReservationMutationReceiptDto>(
                    ReservationsApplicationErrors.ManagementOperationConflict);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result applied = apply(reservation, nowUtc);
        if (applied.IsFailure)
        {
            return Result.Failure<ReservationMutationReceiptDto>(applied.Error);
        }

        await operations.AddAsync(
            new ReservationManagementOperationRecord(
                operationId,
                reservation.ScopeId,
                propertyId,
                reservationId,
                kind,
                expectedVersion,
                ExpectedDetailsRevision: null,
                businessDate,
                nowUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(reservation.ToMutationReceipt());
    }

    private static bool IsValidRequest(
        Guid operationId,
        ReservationManagementOperationKind kind,
        DateOnly? businessDate,
        string? actorId)
    {
        bool isCancellation = kind == ReservationManagementOperationKind.Cancel;
        if (operationId == Guid.Empty ||
            kind == ReservationManagementOperationKind.Unknown ||
            !Enum.IsDefined(kind) ||
            isCancellation == businessDate.HasValue)
        {
            return false;
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        return isCancellation
            ? normalizedActor.Length <= Reservation.ActorIdMaxLength
            : normalizedActor.Length is > 0 and <= Reservation.ActorIdMaxLength;
    }
}
