namespace BunkFy.Modules.Reservations.Application.Queries;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record ListReservationDataHoldsQuery(
    Guid PropertyId,
    Guid ReservationId,
    ReservationDataHoldStatus? Status,
    int Page,
    int PageSize)
    : IQuery<ReservationDataHoldListResponse>;
