namespace BunkFy.Modules.Guests.Application.Commands;

using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Cqrs;

public sealed record PlaceGuestDataHoldCommand(
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid GuestId,
    long ExpectedGuestVersion,
    string ReasonCode,
    string ActorId)
    : ITransactionalCommand<GuestDataHoldReceiptDto>;
