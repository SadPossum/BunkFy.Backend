namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class GetReservationQueryHandler(IReservationRepository reservations)
    : IQueryHandler<GetReservationQuery, ReservationDto>
{
    public async Task<Result<ReservationDto>> HandleAsync(GetReservationQuery query, CancellationToken cancellationToken)
    {
        Reservation? reservation = await reservations
            .GetAsync(query.PropertyId, query.ReservationId, cancellationToken)
            .ConfigureAwait(false);
        return reservation is null
            ? Result.Failure<ReservationDto>(ReservationsApplicationErrors.ReservationNotFound)
            : Result.Success(reservation.ToDto());
    }
}
