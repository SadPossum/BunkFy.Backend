namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record PlaceReservationDataHoldCommand(
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid ReservationId,
    long ExpectedReservationVersion,
    long ExpectedDetailsRevision,
    string ReasonCode,
    string ActorId)
    : ITransactionalCommand<ReservationDataHoldReceiptDto>;
