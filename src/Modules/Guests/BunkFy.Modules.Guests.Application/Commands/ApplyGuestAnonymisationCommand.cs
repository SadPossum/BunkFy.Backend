namespace BunkFy.Modules.Guests.Application.Commands;

using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Cqrs;

internal sealed record ApplyGuestAnonymisationCommand(
    Guid IdempotencyKey,
    Guid RoutingPropertyId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid GuestId,
    long ExpectedGuestVersion,
    GuestAnonymisationRoutingPolicyEvidence RoutingPolicy,
    string ActorId) : ITransactionalCommand<GuestAnonymisationReceiptDto>;
