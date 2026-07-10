namespace Reservations.Application.Queries;

using Gma.Framework.Cqrs;
using Reservations.Contracts;

public sealed record ListReservationsQuery(
    Guid PropertyId,
    ReservationStatus? Status,
    int Page,
    int PageSize)
    : IQuery<ReservationListResponse>;
