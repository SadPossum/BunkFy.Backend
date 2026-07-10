namespace Reservations.Application.Commands;

using Gma.Framework.Cqrs;
using Reservations.Contracts;

public sealed record CancelReservationCommand(
    Guid PropertyId,
    Guid ReservationId,
    long ExpectedVersion)
    : ITransactionalCommand<ReservationDto>;
