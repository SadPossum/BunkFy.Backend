namespace BunkFy.Modules.Guests.Application.Commands;

using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyGuestProcessingRestrictionCommand(
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid CaseId,
    long ApprovalRevision,
    Guid GuestId,
    long ExpectedGuestVersion,
    long ExpectedProjectionRevision,
    string ActorId) : ITransactionalCommand<GuestProcessingRestrictionReceiptDto>;
