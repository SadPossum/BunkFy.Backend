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
    IReservationRepository reservations,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<CheckInReservationCommand, ReservationDto>
{
    public async Task<Result<ReservationDto>> HandleAsync(
        CheckInReservationCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations.GetAsync(
            command.PropertyId, command.ReservationId, cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        Result changed = reservation.CheckIn(
            command.ExpectedVersion,
            command.BusinessDate,
            command.ActorId,
            ids.NewId(),
            clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(reservation.ToDto())
            : Result.Failure<ReservationDto>(changed.Error);
    }
}

internal sealed class MarkReservationNoShowCommandHandler(
    IReservationRepository reservations,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<MarkReservationNoShowCommand, ReservationDto>
{
    public async Task<Result<ReservationDto>> HandleAsync(
        MarkReservationNoShowCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations.GetAsync(
            command.PropertyId, command.ReservationId, cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        Result changed = reservation.RequestNoShow(
            command.ExpectedVersion,
            command.BusinessDate,
            command.ActorId,
            ids.NewId(),
            ids.NewId(),
            clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(reservation.ToDto())
            : Result.Failure<ReservationDto>(changed.Error);
    }
}

internal sealed class CheckOutReservationCommandHandler(
    IReservationRepository reservations,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<CheckOutReservationCommand, ReservationDto>
{
    public async Task<Result<ReservationDto>> HandleAsync(
        CheckOutReservationCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations.GetAsync(
            command.PropertyId, command.ReservationId, cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        Result changed = reservation.RequestCheckout(
            command.ExpectedVersion,
            command.BusinessDate,
            command.ActorId,
            ids.NewId(),
            ids.NewId(),
            clock.UtcNow);
        return changed.IsSuccess
            ? Result.Success(reservation.ToDto())
            : Result.Failure<ReservationDto>(changed.Error);
    }
}
