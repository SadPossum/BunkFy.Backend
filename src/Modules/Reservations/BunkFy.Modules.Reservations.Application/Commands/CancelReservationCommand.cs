namespace BunkFy.Modules.Reservations.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Contracts;

public sealed record CancelReservationCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId,
    long ExpectedVersion,
    string? ActorId = null)
    : ITransactionalCommand<ReservationMutationReceiptDto>;
