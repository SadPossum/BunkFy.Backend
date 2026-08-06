namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.DataRights;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class BeginIngestionAnonymisationCommandHandler(
    IIngestionAnonymisationRestoreRepository repository,
    IngestionSourceMutationCoordinator sourceMutations,
    IIngestionAnonymisationFingerprintService fingerprintService,
    IIngestionAnonymisationEligibilityEvaluator eligibility,
    IDataRightsOperationApprovalGate approvalGate,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        BeginIngestionAnonymisationCommand,
        IngestionAnonymisationExecutionStage>
{
    public async Task<Result<IngestionAnonymisationExecutionStage>>
        HandleAsync(
            BeginIngestionAnonymisationCommand command,
            CancellationToken cancellationToken)
    {
        string? tenantId =
            scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        DataRightsAnonymisationContributionRequest? request =
            command.Request;
        DateTimeOffset nowUtc = clock.UtcNow;
        if (string.IsNullOrWhiteSpace(tenantId) ||
            !IngestionAnonymisationExecutionRequestValidator
                .IsValid(request, tenantId, nowUtc))
        {
            return InvalidRequest();
        }

        IngestionSourceMutationLease? lease =
            await sourceMutations.AcquireSourceLinkAsync(
                    request.Coordinate.RecordId,
                    cancellationToken)
                .ConfigureAwait(false);
        if (lease is null)
        {
            return Result.Failure<
                IngestionAnonymisationExecutionStage>(
                    IngestionApplicationErrors.AnonymisationBlocked(
                        IngestionAnonymisationBlockerCode
                            .SourceLinkNotFound));
        }

        IngestionAnonymisationRoutingPolicyEvidence routingPolicy =
            IngestionAnonymisationPolicyEvidence.FromApproval(
                request.RoutingPolicy);
        string approvalEvidenceSha256 =
            IngestionAnonymisationPolicyEvidence.ComputeSha256(
                routingPolicy);
        IngestionAnonymisationTombstone? existing =
            await repository.GetTombstoneAsync(
                request.Coordinate.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ResumeAsync(
                existing,
                request,
                approvalEvidenceSha256,
                cancellationToken).ConfigureAwait(false);
        }

        DataRightsOperationApprovalResult approval =
            await approvalGate.EvaluateAsync(
                new(
                    tenantId,
                    request.RoutingPropertyId,
                    request.CaseId,
                    request.ApprovalRevision,
                    DataRightsOperation.Anonymisation,
                    IngestionDataRightsCoordinates.Owner,
                    IngestionDataRightsCoordinates
                        .ReservationSourceLinkRecordType,
                    request.Coordinate.RecordId,
                    request.Coordinate.RecordVersion,
                    ExecutingActorId:
                        request.ExecutingActorId.Trim()),
                cancellationToken).ConfigureAwait(false);
        if (!approval.IsApproved ||
            approval.ApprovalEvidence is null ||
            !IngestionAnonymisationPolicyEvidence.MatchesApproval(
                routingPolicy,
                approval.ApprovalEvidence))
        {
            return Result.Failure<
                IngestionAnonymisationExecutionStage>(
                    IngestionApplicationErrors
                        .AnonymisationApprovalRequired);
        }

        existing = await repository.GetTombstoneAsync(
            request.Coordinate.RecordId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ResumeAsync(
                existing,
                request,
                approvalEvidenceSha256,
                cancellationToken).ConfigureAwait(false);
        }

        IngestionAnonymisationEligibilityResult eligible =
            await eligibility.EvaluateAsync(
                new(
                    IngestionAnonymisationEligibilityContract
                        .CurrentVersion,
                    tenantId,
                    request.CaseId,
                    request.ApprovalRevision,
                    request.OperationRevision,
                    request.RoutingPropertyId,
                    request.Coordinate.RecordId,
                    request.Coordinate.RecordVersion,
                    routingPolicy),
                cancellationToken).ConfigureAwait(false);
        if (eligible.Status !=
                IngestionAnonymisationEligibilityStatus.Eligible ||
            eligible.BlockerCode !=
                IngestionAnonymisationBlockerCode.None)
        {
            return Result.Failure<
                IngestionAnonymisationExecutionStage>(
                    IngestionApplicationErrors.AnonymisationBlocked(
                        eligible.BlockerCode));
        }

        if (eligible.SourceLinkVersion !=
                request.Coordinate.RecordVersion ||
            eligible.GraphRecordCount <= 0 ||
            eligible.GraphRecordCount >
                IngestionAnonymisationEligibilityContract
                    .MaximumGraphRecords ||
            !IsSha256(eligible.PolicyEvidenceSha256) ||
            !IsSha256(eligible.OperationFenceSha256) ||
            !string.Equals(
                eligible.PolicyEvidenceSha256,
                approvalEvidenceSha256,
                StringComparison.Ordinal))
        {
            return Conflict();
        }

        IngestionAnonymisationRestoreGraphLoadResult loaded =
            await repository.LoadInitialGraphAsync(
                request.RoutingPropertyId,
                request.Coordinate.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (loaded is not
            {
                Status:
                    IngestionAnonymisationRestoreGraphLoadStatus.Found,
                Graph: { } graph
            } ||
            graph.SourceLink.Version !=
                request.Coordinate.RecordVersion ||
            graph.RecordCount != eligible.GraphRecordCount)
        {
            return Conflict();
        }

        DateTimeOffset startedAtUtc =
            ToPersistencePrecision(clock.UtcNow);
        Result<IReadOnlyList<
            IngestionAnonymisationFingerprintValue>>
            fingerprintValues =
                IngestionAnonymisationRestoreFingerprintFactory
                    .CreateValues(
                        fingerprintService,
                        tenantId,
                        graph);
        if (fingerprintValues.IsFailure)
        {
            return Result.Failure<
                IngestionAnonymisationExecutionStage>(
                    fingerprintValues.Error);
        }

        Result<IReadOnlyList<
            IngestionAnonymisationRecordPlanEntry>> planned =
                IngestionAnonymisationRestorePlanBuilder.Create(
                    ids,
                    tenantId,
                    graph,
                    startedAtUtc);
        if (planned.IsFailure)
        {
            return Result.Failure<
                IngestionAnonymisationExecutionStage>(
                    planned.Error);
        }

        Guid ownerReceiptId = ids.NewId();
        Result<IngestionAnonymisationTombstone> tombstone =
            IngestionAnonymisationTombstone.BeginExecution(
                tenantId,
                request.Coordinate.RecordId,
                request.RoutingPropertyId,
                graph.SourceLink.ConnectionId,
                request.Coordinate.RecordVersion,
                request.WorkItemId,
                request.IdempotencyKey,
                request.CaseId,
                request.ApprovalRevision,
                request.OperationRevision,
                ownerReceiptId,
                approvalEvidenceSha256,
                eligible.PolicyEvidenceSha256!,
                eligible.OperationFenceSha256!,
                request.ExecutingActorId,
                graph.RecordCount,
                fingerprintValues.Value.Count,
                planned.Value.Count(entry =>
                    entry.RequiresRawPayloadDeletion),
                startedAtUtc);
        if (tombstone.IsFailure)
        {
            return Result.Failure<
                IngestionAnonymisationExecutionStage>(
                    tombstone.Error);
        }

        Result<IReadOnlyList<
            IngestionAnonymisationFingerprint>> fingerprints =
                IngestionAnonymisationRestoreFingerprintFactory
                    .CreateEntities(
                        ids,
                        tenantId,
                        request.Coordinate.RecordId,
                        fingerprintValues.Value,
                        startedAtUtc);
        if (fingerprints.IsFailure)
        {
            return Result.Failure<
                IngestionAnonymisationExecutionStage>(
                    fingerprints.Error);
        }

        Result reduced =
            IngestionAnonymisationRestoreGraphReducer.Reduce(
                graph,
                ownerReceiptId,
                request.Coordinate.RecordVersion,
                startedAtUtc);
        if (reduced.IsFailure)
        {
            return Result.Failure<
                IngestionAnonymisationExecutionStage>(
                    reduced.Error);
        }

        if (!IngestionAnonymisationStateVerifier.Matches(
                graph,
                planned.Value,
                ownerReceiptId,
                completed: false))
        {
            return Conflict();
        }

        repository.AddRestoreState(
            tombstone.Value,
            fingerprints.Value,
            planned.Value);
        return Result.Success(
            IngestionAnonymisationExecutionStageFactory.Create(
                tombstone.Value,
                planned.Value));
    }

    private async Task<Result<
        IngestionAnonymisationExecutionStage>> ResumeAsync(
            IngestionAnonymisationTombstone tombstone,
            DataRightsAnonymisationContributionRequest request,
            string approvalEvidenceSha256,
            CancellationToken cancellationToken)
    {
        if (!tombstone.MatchesExecution(
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
        bool completed = tombstone.State ==
            IngestionAnonymisationTombstoneState.Completed;
        if (loaded.Graph is null ||
            !IngestionAnonymisationStateVerifier.Matches(
                loaded.Graph,
                plan,
                tombstone.ReductionClaimId,
                completed))
        {
            return Conflict();
        }

        if (!completed)
        {
            return Result.Success(
                IngestionAnonymisationExecutionStageFactory.Create(
                    tombstone,
                    plan));
        }

        IngestionAnonymisationReceipt? receipt =
            await repository.GetReceiptAsync(
                tombstone.OwnerReceiptId,
                cancellationToken).ConfigureAwait(false);
        if (receipt is null ||
            !IngestionAnonymisationExecutionStageFactory
                .MatchesCompletedProof(tombstone, receipt))
        {
            return Result.Failure<
                IngestionAnonymisationExecutionStage>(
                    IngestionApplicationErrors
                        .AnonymisationProofUnavailable);
        }

        return Result.Success(
            IngestionAnonymisationExecutionStageFactory.Create(
                tombstone,
                plan,
                receipt));
    }

    private static bool IsSha256(string? value) =>
        value is
        {
            Length:
                IngestionAnonymisationEligibilityContract
                    .Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<IngestionAnonymisationExecutionStage>
        InvalidRequest() =>
        Result.Failure<IngestionAnonymisationExecutionStage>(
            IngestionApplicationErrors.AnonymisationRequestInvalid);

    private static Result<IngestionAnonymisationExecutionStage>
        Conflict() =>
        Result.Failure<IngestionAnonymisationExecutionStage>(
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
