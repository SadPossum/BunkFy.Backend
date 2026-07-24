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

internal sealed class ListGuestProcessingRestrictionsQueryHandler(
    IGuestProfileRepository profiles,
    IGuestProcessingRestrictionRepository restrictions)
    : IQueryHandler<
        ListGuestProcessingRestrictionsQuery,
        GuestProcessingRestrictionListResponse>
{
    public async Task<Result<GuestProcessingRestrictionListResponse>> HandleAsync(
        ListGuestProcessingRestrictionsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PropertyId == Guid.Empty || query.GuestId == Guid.Empty)
        {
            return Result.Failure<GuestProcessingRestrictionListResponse>(
                GuestsApplicationErrors.RestrictionRequestInvalid);
        }

        GuestProfile? profile = await profiles.GetForDataRightsAsync(
            query.PropertyId,
            query.GuestId,
            cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestProcessingRestrictionListResponse>(
                GuestsApplicationErrors.GuestNotFound);
        }

        PageRequest page = PageRequest.Normalize(query.Page, query.PageSize);
        IReadOnlyCollection<GuestProcessingRestriction> rows =
            await restrictions.ListActiveAsync(
                query.PropertyId,
                query.GuestId,
                page,
                cancellationToken).ConfigureAwait(false);
        return Result.Success(new GuestProcessingRestrictionListResponse(
            rows.Select(restriction => restriction.ToDto()).ToArray(),
            page.Page,
            page.PageSize));
    }
}
