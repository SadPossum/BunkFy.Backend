namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyReservationProcessingRestrictionCommand(
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid CaseId,
    long ApprovalRevision,
    Guid ReservationId,
    long ExpectedReservationVersion,
    long ExpectedProjectionRevision,
    string ActorId)
    : ITransactionalCommand<ReservationProcessingRestrictionReceiptDto>;
