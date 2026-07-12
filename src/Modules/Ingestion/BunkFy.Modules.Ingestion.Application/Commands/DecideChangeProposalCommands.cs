namespace BunkFy.Modules.Ingestion.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Domain.Proposals;

public sealed record AcceptChangeProposalCommand(
    Guid PropertyId,
    Guid ProposalId,
    string Actor,
    long ExpectedProposalVersion,
    long ExpectedReservationDetailsRevision)
    : ITransactionalCommand<ChangeProposalDecisionResult>;

public sealed record RejectChangeProposalCommand(
    Guid PropertyId,
    Guid ProposalId,
    string Actor,
    string Reason,
    long ExpectedProposalVersion)
    : ITransactionalCommand<ChangeProposalDecisionResult>;

public sealed record ChangeProposalDecisionResult(
    Guid ProposalId,
    ChangeProposalState State,
    long Version,
    Guid? ProductOperationId);
