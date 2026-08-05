namespace BunkFy.Modules.Ingestion.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Contracts;

public sealed record AcceptChangeProposalCommand(
    Guid PropertyId,
    Guid ProposalId,
    string Actor,
    long ExpectedProposalVersion,
    long ExpectedReservationDetailsRevision)
    : ITransactionalCommand<ChangeProposalMutationReceiptDto>;

public sealed record RejectChangeProposalCommand(
    Guid PropertyId,
    Guid ProposalId,
    string Actor,
    string Reason,
    long ExpectedProposalVersion)
    : ITransactionalCommand<ChangeProposalMutationReceiptDto>;
