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
    ReservationMutationCoordinator mutations,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CancelReservationCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        CancelReservationCommand command,
        CancellationToken cancellationToken)
    {
        Reservation? reservation = await mutations.AcquireOperationalAsync(
            command.PropertyId,
            command.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.ReservationNotFound);
        }

        Result result = reservation.RequestCancellation(
            command.ExpectedVersion,
            idGenerator.NewId(),
            idGenerator.NewId(),
            clock.UtcNow,
            command.ActorId);
        return result.IsFailure
            ? Result.Failure<ReservationMutationReceiptDto>(result.Error)
            : Result.Success(reservation.ToMutationReceipt());
    }
}
