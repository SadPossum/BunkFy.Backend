namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Reservations.Application.Mapping;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListReservationDataHoldsQueryHandler(
    IReservationRepository reservations,
    IReservationDataHoldRepository holds)
    : IQueryHandler<
        ListReservationDataHoldsQuery,
        ReservationDataHoldListResponse>
{
    public async Task<Result<ReservationDataHoldListResponse>> HandleAsync(
        ListReservationDataHoldsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PropertyId == Guid.Empty ||
            query.ReservationId == Guid.Empty ||
            (query.Status.HasValue &&
             query.Status is not ReservationDataHoldStatus.Active and
                 not ReservationDataHoldStatus.Released))
        {
            return Result.Failure<ReservationDataHoldListResponse>(
                ReservationsApplicationErrors.DataHoldRequestInvalid);
        }

        Reservation? reservation = await reservations.GetForDataRightsAsync(
            query.PropertyId,
            query.ReservationId,
            cancellationToken).ConfigureAwait(false);
        if (reservation is null)
        {
            return Result.Failure<ReservationDataHoldListResponse>(
                ReservationsApplicationErrors.ReservationNotFound);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        IReadOnlyCollection<ReservationDataHold> rows = await holds.ListAsync(
            query.PropertyId,
            query.ReservationId,
            query.Status,
            page,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new ReservationDataHoldListResponse(
            rows.Select(hold => hold.ToDto()).ToArray(),
            page.Page,
            page.PageSize));
    }
}
