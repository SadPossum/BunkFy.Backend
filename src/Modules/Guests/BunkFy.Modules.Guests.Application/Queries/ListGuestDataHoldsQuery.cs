namespace BunkFy.Modules.Guests.Application.Queries;

using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Cqrs;

public sealed record ListGuestDataHoldsQuery(
    Guid PropertyId,
    Guid GuestId,
    GuestDataHoldStatus? Status,
    int Page,
    int PageSize)
    : IQuery<GuestDataHoldListResponse>;
