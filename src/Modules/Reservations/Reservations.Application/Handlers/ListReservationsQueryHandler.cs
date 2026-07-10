namespace Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Reservations.Application.Ports;
using Reservations.Application.Queries;
using Reservations.Contracts;

internal sealed class ListReservationsQueryHandler(IReservationRepository reservations)
    : IQueryHandler<ListReservationsQuery, ReservationListResponse>
{
    public async Task<Result<ReservationListResponse>> HandleAsync(
        ListReservationsQuery query,
        CancellationToken cancellationToken) =>
        Result.Success(await reservations.ListAsync(
            query.PropertyId,
            query.Status,
            PageRequest.Normalize(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
}
