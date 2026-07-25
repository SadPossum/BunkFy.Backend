namespace BunkFy.Modules.Guests.Application.Commands;

using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Cqrs;

public sealed record ReleaseGuestDataHoldCommand(
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid GuestId,
    Guid HoldId,
    long ExpectedGuestVersion,
    long ExpectedHoldVersion,
    string ActorId)
    : ITransactionalCommand<GuestDataHoldReceiptDto>;
