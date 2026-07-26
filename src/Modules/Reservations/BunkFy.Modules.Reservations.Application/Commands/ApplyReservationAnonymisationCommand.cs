namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

public sealed record ApplyReservationAnonymisationCommand(
    Guid IdempotencyKey,
    Guid PropertyId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid ReservationId,
    long ExpectedReservationVersion,
    long ExpectedDetailsRevision,
    ReservationAnonymisationRoutingPolicyEvidence RoutingPolicy,
    string ActorId)
    : ITransactionalCommand<ReservationAnonymisationReceiptDto>;
