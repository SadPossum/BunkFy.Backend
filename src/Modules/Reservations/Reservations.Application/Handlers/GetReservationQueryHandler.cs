namespace Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Reservations.Application.Ports;
using Reservations.Application.Queries;
using Reservations.Contracts;
using Reservations.Domain.Aggregates;

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
