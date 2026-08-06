namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.DataRights;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class
    CompleteIngestionAnonymisationCommandHandler(
        IIngestionAnonymisationRestoreRepository repository,
        IngestionSourceMutationCoordinator sourceMutations,
        IRawPayloadStore rawPayloads,
        IScopeContext scopeContext,
        ISystemClock clock)
    : ICommandHandler<
        CompleteIngestionAnonymisationCommand,
        IngestionAnonymisationExecutionProof>
{
    public async Task<Result<
        IngestionAnonymisationExecutionProof>> HandleAsync(
            CompleteIngestionAnonymisationCommand command,
            CancellationToken cancellationToken)
    {
        string? tenantId =
            scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        DataRightsAnonymisationContributionRequest? request =
            command.Request;
        if (string.IsNullOrWhiteSpace(tenantId) ||
            !IngestionAnonymisationExecutionRequestValidator
                .IsValid(request, tenantId, clock.UtcNow))
        {
            return InvalidRequest();
        }

        IngestionAnonymisationRoutingPolicyEvidence routingPolicy =
            IngestionAnonymisationPolicyEvidence.FromApproval(
                request.RoutingPolicy);
        string approvalEvidenceSha256 =
            IngestionAnonymisationPolicyEvidence.ComputeSha256(
                routingPolicy);

        IngestionSourceMutationLease? lease =
            await sourceMutations.AcquireSourceLinkAsync(
                    request.Coordinate.RecordId,
                    cancellationToken)
                .ConfigureAwait(false);
        if (lease is null)
        {
            return Conflict();
        }

        IngestionAnonymisationTombstone? tombstone =
            await repository.GetTombstoneAsync(
                request.Coordinate.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (tombstone is null ||
            !tombstone.MatchesExecution(
                request.WorkItemId,
                request.IdempotencyKey,
                request.RoutingPropertyId,
                request.CaseId,
                request.ApprovalRevision,
                request.OperationRevision,
                request.Coordinate.RecordVersion,
                approvalEvidenceSha256,
                request.ExecutingActorId))
        {
            return Conflict();
        }

        IReadOnlyList<IngestionAnonymisationRecordPlanEntry> plan =
            await repository.GetPlanAsync(
                tombstone.Id,
                cancellationToken).ConfigureAwait(false);
        int fingerprintCount =
            await repository.CountFingerprintsAsync(
                tombstone.Id,
                cancellationToken).ConfigureAwait(false);
        if (plan.Count != tombstone.GraphRecordCount ||
            plan.Count(entry =>
                entry.RequiresRawPayloadDeletion) !=
                    tombstone.RawPayloadCount ||
            fingerprintCount != tombstone.FingerprintCount)
        {
            return Conflict();
        }

        IngestionAnonymisationRestoreGraphLoadResult loaded =
            await repository.LoadPlannedGraphAsync(
                request.RoutingPropertyId,
                request.Coordinate.RecordId,
                plan,
                cancellationToken).ConfigureAwait(false);
        if (loaded.Graph is null)
        {
            return Conflict();
        }

        bool completed = tombstone.State ==
            IngestionAnonymisationTombstoneState.Completed;
        if (!IngestionAnonymisationStateVerifier.Matches(
                loaded.Graph,
                plan,
                tombstone.ReductionClaimId,
                completed))
        {
            return Conflict();
        }

        if (completed)
        {
            return await this.ReplayProofAsync(
                tombstone,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (IngestionAnonymisationRecordPlanEntry entry in
                 plan.Where(item =>
                     item.RequiresRawPayloadDeletion))
        {
            RawPayloadRead? raw = await rawPayloads.ReadAsync(
                entry.RawPayloadFileId!.Value,
                tenantId,
                entry.RawPayloadConnectionId!.Value,
                cancellationToken).ConfigureAwait(false);
            if (raw is not null)
            {
                return Result.Failure<
                    IngestionAnonymisationExecutionProof>(
                        IngestionApplicationErrors
                            .AnonymisationRawPayloadDeletionIncomplete);
            }
        }

        DateTimeOffset completedAtUtc =
            ToPersistencePrecision(clock.UtcNow);
        Dictionary<Guid, ObservationReceipt> receipts =
            loaded.Graph.Receipts.ToDictionary(
                receipt => receipt.Id);
        foreach (IngestionAnonymisationRecordPlanEntry entry in
                 plan.Where(item =>
                     item.RequiresRawPayloadDeletion))
        {
            if (!receipts.TryGetValue(
                    entry.RecordId,
                    out ObservationReceipt? receipt))
            {
                return Conflict();
            }

            Result purged = receipt.CompleteRawPayloadPurge(
                tombstone.ReductionClaimId,
                completedAtUtc);
            if (purged.IsFailure)
            {
                return Result.Failure<
                    IngestionAnonymisationExecutionProof>(
                        purged.Error);
            }
        }

        if (!IngestionAnonymisationStateVerifier.Matches(
                loaded.Graph,
                plan,
                tombstone.ReductionClaimId,
                completed: true))
        {
            return Conflict();
        }

        Result<IngestionAnonymisationReceipt> created =
            IngestionAnonymisationReceipt.Create(
                tombstone.OwnerReceiptId,
                tenantId,
                tombstone.WorkItemId,
                tombstone.IdempotencyKey,
                tombstone.PropertyId,
                tombstone.CaseId,
                tombstone.ApprovalRevision,
                tombstone.OperationRevision,
                tombstone.Id,
                tombstone.SelectedSourceLinkVersion,
                tombstone.ResultingSourceLinkVersion,
                tombstone.GraphRecordCount,
                tombstone.FingerprintCount,
                tombstone.RawPayloadCount,
                tombstone.ApprovalEvidenceSha256,
                tombstone.PolicyEvidenceSha256,
                tombstone.OperationFenceSha256,
                tombstone.ActorId,
                completedAtUtc);
        if (created.IsFailure)
        {
            return Result.Failure<
                IngestionAnonymisationExecutionProof>(
                    created.Error);
        }

        Result finalized =
            tombstone.CompleteExecution(created.Value);
        if (finalized.IsFailure)
        {
            return Result.Failure<
                IngestionAnonymisationExecutionProof>(
                    finalized.Error);
        }

        repository.AddReceipt(created.Value);
        return Result.Success(
            IngestionAnonymisationExecutionStageFactory.CreateProof(
                created.Value));
    }

    private async Task<Result<
        IngestionAnonymisationExecutionProof>> ReplayProofAsync(
            IngestionAnonymisationTombstone tombstone,
            CancellationToken cancellationToken)
    {
        IngestionAnonymisationReceipt? receipt =
            await repository.GetReceiptAsync(
                tombstone.OwnerReceiptId,
                cancellationToken).ConfigureAwait(false);
        return receipt is not null &&
            IngestionAnonymisationExecutionStageFactory
                .MatchesCompletedProof(tombstone, receipt)
            ? Result.Success(
                IngestionAnonymisationExecutionStageFactory
                    .CreateProof(receipt))
            : Result.Failure<
                IngestionAnonymisationExecutionProof>(
                    IngestionApplicationErrors
                        .AnonymisationProofUnavailable);
    }

    private static Result<IngestionAnonymisationExecutionProof>
        InvalidRequest() =>
        Result.Failure<IngestionAnonymisationExecutionProof>(
            IngestionApplicationErrors.AnonymisationRequestInvalid);

    private static Result<IngestionAnonymisationExecutionProof>
        Conflict() =>
        Result.Failure<IngestionAnonymisationExecutionProof>(
            IngestionApplicationErrors
                .AnonymisationExecutionConflict);

    private static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        DateTimeOffset utc = value.ToUniversalTime();
        return new(
            utc.Ticks - (utc.Ticks % ticksPerMicrosecond),
            TimeSpan.Zero);
    }
}
