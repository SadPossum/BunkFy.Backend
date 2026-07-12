namespace BunkFy.Modules.Reservations.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Contracts;

public sealed record CancelReservationCommand(
    Guid PropertyId,
    Guid ReservationId,
    long ExpectedVersion)
    : ITransactionalCommand<ReservationDto>;
