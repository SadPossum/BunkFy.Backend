namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record ReleaseReservationDataHoldCommand(
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid ReservationId,
    Guid HoldId,
    long ExpectedReservationVersion,
    long ExpectedDetailsRevision,
    long ExpectedHoldVersion,
    string ActorId)
    : ITransactionalCommand<ReservationDataHoldReceiptDto>;
