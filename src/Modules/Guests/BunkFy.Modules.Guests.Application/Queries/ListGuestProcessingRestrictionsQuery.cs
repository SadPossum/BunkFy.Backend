namespace BunkFy.Modules.Guests.Application.Queries;

using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Cqrs;

public sealed record ListGuestProcessingRestrictionsQuery(
    Guid PropertyId,
    Guid GuestId,
    int Page,
    int PageSize) : IQuery<GuestProcessingRestrictionListResponse>;
