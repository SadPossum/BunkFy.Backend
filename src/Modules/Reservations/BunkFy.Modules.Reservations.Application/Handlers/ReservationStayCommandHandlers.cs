namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class CheckInReservationCommandHandler(
    ReservationMutationCoordinator mutations,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<CheckInReservationCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        CheckInReservationCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await mutations.AcquireOperationalAsync(
            command.PropertyId, command.ReservationId, cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        Result changed = reservation.CheckIn(
            command.ExpectedVersion,
            command.BusinessDate,
            command.ActorId,
            ids.NewId(),
            clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(reservation.ToMutationReceipt())
            : Result.Failure<ReservationMutationReceiptDto>(changed.Error);
    }
}

internal sealed class MarkReservationNoShowCommandHandler(
    ReservationMutationCoordinator mutations,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<MarkReservationNoShowCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        MarkReservationNoShowCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await mutations.AcquireOperationalAsync(
            command.PropertyId, command.ReservationId, cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        Result changed = reservation.RequestNoShow(
            command.ExpectedVersion,
            command.BusinessDate,
            command.ActorId,
            ids.NewId(),
            ids.NewId(),
            clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(reservation.ToMutationReceipt())
            : Result.Failure<ReservationMutationReceiptDto>(changed.Error);
    }
}

internal sealed class CheckOutReservationCommandHandler(
    ReservationMutationCoordinator mutations,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<CheckOutReservationCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        CheckOutReservationCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await mutations.AcquireOperationalAsync(
            command.PropertyId, command.ReservationId, cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        Result changed = reservation.RequestCheckout(
            command.ExpectedVersion,
            command.BusinessDate,
            command.ActorId,
            ids.NewId(),
            ids.NewId(),
            clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(reservation.ToMutationReceipt())
            : Result.Failure<ReservationMutationReceiptDto>(changed.Error);
    }
}
