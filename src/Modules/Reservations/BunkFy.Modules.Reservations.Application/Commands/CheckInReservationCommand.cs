namespace BunkFy.Modules.Reservations.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Contracts;

public sealed record CheckInReservationCommand(
    Guid PropertyId,
    Guid ReservationId,
    DateOnly BusinessDate,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<ReservationDto>;
