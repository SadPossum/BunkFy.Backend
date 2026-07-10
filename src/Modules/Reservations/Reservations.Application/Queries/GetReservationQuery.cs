namespace Reservations.Application.Queries;

using Gma.Framework.Cqrs;
using Reservations.Contracts;

public sealed record GetReservationQuery(Guid PropertyId, Guid ReservationId) : IQuery<ReservationDto>;
