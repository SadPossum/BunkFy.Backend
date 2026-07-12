namespace BunkFy.Modules.Guests.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Guests.Contracts;

public sealed record ListGuestProfilesQuery(
    Guid PropertyId,
    string? Search,
    GuestStatus? Status,
    int Page,
    int PageSize) : IQuery<GuestListResponse>;
