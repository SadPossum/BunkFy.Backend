namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class CancelReservationCommandHandler(
    IReservationRepository reservations,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CancelReservationCommand, ReservationDto>
{
    public async Task<Result<ReservationDto>> HandleAsync(
        CancelReservationCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations
            .GetAsync(command.PropertyId, command.ReservationId, cancellationToken)
            .ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        Result result = reservation.RequestCancellation(
            command.ExpectedVersion,
            idGenerator.NewId(),
            idGenerator.NewId(),
            clock.UtcNow,
            command.ActorId);
        return result.IsFailure
            ? Result.Failure<ReservationDto>(result.Error)
            : Result.Success(reservation.ToDto());
    }
}
