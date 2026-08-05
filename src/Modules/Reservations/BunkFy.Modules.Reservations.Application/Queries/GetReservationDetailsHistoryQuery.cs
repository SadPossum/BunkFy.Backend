namespace BunkFy.Modules.Reservations.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Contracts;

public sealed record GetReservationDetailsHistoryQuery(
    Guid PropertyId,
    Guid ReservationId,
    int Page,
    int PageSize)
    : IQuery<ReservationDetailsHistoryListResponse>;
