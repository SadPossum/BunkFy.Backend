namespace BunkFy.Modules.Guests.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using BunkFy.Modules.Guests.Application.Mapping;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Application.Queries;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;

internal sealed class GetGuestProfileQueryHandler(IGuestProfileRepository profiles)
    : IQueryHandler<GetGuestProfileQuery, GuestProfileDto>
{
    public async Task<Result<GuestProfileDto>> HandleAsync(
        GetGuestProfileQuery query,
        CancellationToken cancellationToken)
    {
        GuestProfile? profile = await profiles.GetVisibleAsync(
            query.PropertyId, query.GuestId, cancellationToken).ConfigureAwait(false);
        return profile is null
            ? Result.Failure<GuestProfileDto>(GuestsApplicationErrors.GuestNotFound)
            : Result.Success(profile.ToDto());
    }
}

internal sealed class ListGuestProfilesQueryHandler(IGuestProfileRepository profiles)
    : IQueryHandler<ListGuestProfilesQuery, GuestListResponse>
{
    public async Task<Result<GuestListResponse>> HandleAsync(
        ListGuestProfilesQuery query,
        CancellationToken cancellationToken) => Result.Success(await profiles.ListVisibleAsync(
        query.PropertyId,
        query.Search,
        query.Status,
        PageRequest.Normalize(query.Page, query.PageSize),
        cancellationToken).ConfigureAwait(false));
}

internal sealed class GetGuestStayHistoryQueryHandler(
    IGuestProfileRepository profiles,
    IGuestStayHistoryRepository stays)
    : IQueryHandler<GetGuestStayHistoryQuery, IReadOnlyCollection<GuestStayHistoryItem>>
{
    public async Task<Result<IReadOnlyCollection<GuestStayHistoryItem>>> HandleAsync(
        GetGuestStayHistoryQuery query,
        CancellationToken cancellationToken)
    {
        GuestProfile? profile = await profiles.GetVisibleAsync(
            query.PropertyId,
            query.GuestId,
            cancellationToken).ConfigureAwait(false);
        return profile is null
            ? Result.Failure<IReadOnlyCollection<GuestStayHistoryItem>>(GuestsApplicationErrors.GuestNotFound)
            : Result.Success(await stays.ListAsync(
                query.PropertyId,
                query.GuestId,
                cancellationToken).ConfigureAwait(false));
    }
}
