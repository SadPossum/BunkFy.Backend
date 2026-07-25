namespace BunkFy.Modules.Guests.Application.Handlers;

using BunkFy.Modules.Guests.Application.Mapping;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Application.Queries;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListGuestDataHoldsQueryHandler(
    IGuestProfileRepository profiles,
    IGuestDataHoldRepository holds)
    : IQueryHandler<ListGuestDataHoldsQuery, GuestDataHoldListResponse>
{
    public async Task<Result<GuestDataHoldListResponse>> HandleAsync(
        ListGuestDataHoldsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PropertyId == Guid.Empty ||
            query.GuestId == Guid.Empty ||
            (query.Status.HasValue &&
             query.Status is not GuestDataHoldStatus.Active and not GuestDataHoldStatus.Released))
        {
            return Result.Failure<GuestDataHoldListResponse>(
                GuestsApplicationErrors.DataHoldRequestInvalid);
        }

        GuestProfile? profile = await profiles.GetForDataRightsAsync(
            query.PropertyId,
            query.GuestId,
            cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestDataHoldListResponse>(
                GuestsApplicationErrors.GuestNotFound);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        IReadOnlyCollection<GuestDataHold> rows = await holds.ListAsync(
            query.PropertyId,
            query.GuestId,
            query.Status,
            page,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new GuestDataHoldListResponse(
            rows.Select(hold => hold.ToDto()).ToArray(),
            page.Page,
            page.PageSize));
    }
}
