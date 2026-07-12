namespace BunkFy.Modules.Reservations.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Contracts;

public sealed record GetReservationQuery(Guid PropertyId, Guid ReservationId) : IQuery<ReservationDto>;
