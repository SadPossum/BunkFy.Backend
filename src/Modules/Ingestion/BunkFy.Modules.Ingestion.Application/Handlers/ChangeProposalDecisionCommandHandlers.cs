namespace BunkFy.Modules.Ingestion.Application.Handlers;

using System.Text.Json;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Reservations;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reservations;

internal sealed class AcceptChangeProposalCommandHandler(
    IChangeProposalRepository proposals,
    IObservationReceiptRepository receipts,
    IReservationSourceLinkRepository sourceLinks,
    IReservationDispatchRepository dispatches,
    ReservationObservationPayloadLoader payloadLoader,
    ReservationExternalRequestPublisher requestPublisher,
    ISystemClock clock)
    : ICommandHandler<AcceptChangeProposalCommand, ChangeProposalDecisionResult>
{
    public async Task<Result<ChangeProposalDecisionResult>> HandleAsync(
        AcceptChangeProposalCommand command,
        CancellationToken cancellationToken)
    {
        ChangeProposal? proposal = await proposals.GetAsync(command.ProposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null || proposal.PropertyId != command.PropertyId)
        {
            return Result.Failure<ChangeProposalDecisionResult>(IngestionApplicationErrors.ProposalNotFound);
        }

        Guid requestedOperationId = ReservationOperationIdentity.CreateProposalOperationId(
            proposal.Id,
            command.ExpectedProposalVersion,
            command.ExpectedReservationDetailsRevision);
        ReservationDispatch? existing = await dispatches.FindByTriggerAsync(
            ReservationDispatchTriggerKind.Proposal,
            proposal.Id,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return proposal.ProductOperationId == existing.Id && existing.Id == requestedOperationId
                ? Result.Success(ToResult(proposal))
                : Result.Failure<ChangeProposalDecisionResult>(IngestionApplicationErrors.ProposalDecisionConflict);
        }

        if (proposal.State != ChangeProposalState.Pending || command.ExpectedReservationDetailsRevision <= 0)
        {
            return Result.Failure<ChangeProposalDecisionResult>(IngestionApplicationErrors.ProposalDecisionConflict);
        }

        ObservationReceipt? receipt = await receipts.GetAsync(proposal.ReceiptId, cancellationToken).ConfigureAwait(false);
        ReservationSourceLink? link = await sourceLinks.FindByReservationAsync(proposal.ReservationId, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is null || link is null || receipt.ConnectionId != proposal.ConnectionId ||
            receipt.PropertyId != proposal.PropertyId || link.ConnectionId != proposal.ConnectionId ||
            link.PropertyId != proposal.PropertyId || link.ReservationId != proposal.ReservationId ||
            link.State != ReservationSourceLinkState.Linked || link.ActiveProductOperationId.HasValue)
        {
            return Result.Failure<ChangeProposalDecisionResult>(IngestionApplicationErrors.ProposalDecisionConflict);
        }

        Result<NormalizedReservationObservation> loaded = await payloadLoader.LoadAsync(receipt, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure<ChangeProposalDecisionResult>(loaded.Error);
        }

        ReservationDispatchKind kind = ReservationObservationDispatchClassifier.Classify(link, loaded.Value);
        Guid operationId = requestedOperationId;
        Result<ReservationDispatch> created = ReservationDispatch.Create(
            operationId,
            proposal.ScopeId,
            link.Id,
            ReservationDispatchTriggerKind.Proposal,
            proposal.Id,
            receipt.Id,
            proposal.ConnectionId,
            proposal.PropertyId,
            proposal.ReservationId,
            kind,
            receipt.SourceRevision,
            loaded.Value.SourceSequence,
            JsonSerializer.Serialize(loaded.Value),
            command.ExpectedReservationDetailsRevision,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<ChangeProposalDecisionResult>(created.Error);
        }

        Result applying = proposal.BeginApply(
            command.Actor,
            operationId,
            command.ExpectedProposalVersion,
            clock.UtcNow);
        if (applying.IsFailure)
        {
            return Result.Failure<ChangeProposalDecisionResult>(applying.Error);
        }

        Result begun = link.BeginDispatch(operationId, clock.UtcNow);
        if (begun.IsFailure)
        {
            return Result.Failure<ChangeProposalDecisionResult>(begun.Error);
        }

        await dispatches.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        await requestPublisher.PublishAsync(
            receipt,
            link,
            operationId,
            kind,
            command.ExpectedReservationDetailsRevision,
            loaded.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(ToResult(proposal));
    }

    private static ChangeProposalDecisionResult ToResult(ChangeProposal proposal) =>
        new(proposal.Id, proposal.State, proposal.Version, proposal.ProductOperationId);
}

internal sealed class RejectChangeProposalCommandHandler(
    IChangeProposalRepository proposals,
    IIngestionRetentionPolicy retentionPolicy,
    ISystemClock clock)
    : ICommandHandler<RejectChangeProposalCommand, ChangeProposalDecisionResult>
{
    public async Task<Result<ChangeProposalDecisionResult>> HandleAsync(
        RejectChangeProposalCommand command,
        CancellationToken cancellationToken)
    {
        ChangeProposal? proposal = await proposals.GetAsync(command.ProposalId, cancellationToken).ConfigureAwait(false);
        if (proposal is null || proposal.PropertyId != command.PropertyId)
        {
            return Result.Failure<ChangeProposalDecisionResult>(IngestionApplicationErrors.ProposalNotFound);
        }

        string actor = command.Actor?.Trim() ?? string.Empty;
        string reason = command.Reason?.Trim() ?? string.Empty;
        if (proposal.State == ChangeProposalState.Rejected &&
            string.Equals(proposal.DecisionActor, actor, StringComparison.Ordinal) &&
            string.Equals(proposal.DecisionReason, reason, StringComparison.Ordinal))
        {
            return Result.Success(ToResult(proposal));
        }

        if (proposal.State != ChangeProposalState.Pending)
        {
            return Result.Failure<ChangeProposalDecisionResult>(IngestionApplicationErrors.ProposalDecisionConflict);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result rejected = proposal.Reject(
            actor,
            reason,
            command.ExpectedProposalVersion,
            retentionPolicy.GetSensitiveHistoryRetainUntilUtc(
                proposal.PropertyId,
                proposal.ConnectionId,
                nowUtc),
            nowUtc);
        return rejected.IsSuccess
            ? Result.Success(ToResult(proposal))
            : Result.Failure<ChangeProposalDecisionResult>(rejected.Error);
    }

    private static ChangeProposalDecisionResult ToResult(ChangeProposal proposal) =>
        new(proposal.Id, proposal.State, proposal.Version, proposal.ProductOperationId);
}
