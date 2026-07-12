namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class GetReservationDetailsHistoryQueryHandler(
    IReservationRepository reservations,
    IReservationDetailsHistoryReader history)
    : IQueryHandler<GetReservationDetailsHistoryQuery, IReadOnlyList<ReservationDetailsHistoryItem>>
{
    public async Task<Result<IReadOnlyList<ReservationDetailsHistoryItem>>> HandleAsync(
        GetReservationDetailsHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (await reservations.GetAsync(query.PropertyId, query.ReservationId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<ReservationDetailsHistoryItem>>(
                ReservationsApplicationErrors.ReservationNotFound);
        }

        return Result.Success(await history.ListAsync(
            query.PropertyId,
            query.ReservationId,
            cancellationToken).ConfigureAwait(false));
    }
}
