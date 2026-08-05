namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class GetReservationDetailsHistoryQueryHandler(
    IReservationRepository reservations,
    IReservationDetailsHistoryReader history)
    : IQueryHandler<GetReservationDetailsHistoryQuery, ReservationDetailsHistoryListResponse>
{
    public async Task<Result<ReservationDetailsHistoryListResponse>> HandleAsync(
        GetReservationDetailsHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (!await reservations.ExistsAsync(
                query.PropertyId,
                query.ReservationId,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ReservationDetailsHistoryListResponse>(
                ReservationsApplicationErrors.ReservationNotFound);
        }

        return Result.Success(await history.ListAsync(
            query.PropertyId,
            query.ReservationId,
            PageRequest.Normalize(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
    }
}
