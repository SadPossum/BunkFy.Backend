namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.DataRights;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class CompleteIngestionAnonymisationRestoreCommandHandler(
    IIngestionAnonymisationRestoreRepository repository,
    IIngestionSourceOperationLock operationLock,
    IRawPayloadStore rawPayloads,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<
        CompleteIngestionAnonymisationRestoreCommand,
        DataRightsAnonymisationRestoreProof>
{
    public async Task<Result<DataRightsAnonymisationRestoreProof>>
        HandleAsync(
            CompleteIngestionAnonymisationRestoreCommand command,
            CancellationToken cancellationToken)
    {
        string? tenantId =
            scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        DataRightsAnonymisationRestoreRequest? request = command.Request;
        if (string.IsNullOrWhiteSpace(tenantId) ||
            !IngestionAnonymisationRestoreRequestValidator.IsValid(
                request,
                tenantId))
        {
            return InvalidRequest();
        }

        await operationLock.AcquireAsync(
            tenantId,
            request.RecordId,
            cancellationToken).ConfigureAwait(false);
        IngestionAnonymisationTombstone? tombstone =
            await repository.GetTombstoneAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (tombstone is null || !Matches(tombstone, request))
        {
            return Conflict();
        }

        IReadOnlyList<IngestionAnonymisationRecordPlanEntry> plan =
            await repository.GetPlanAsync(
                tombstone.Id,
                cancellationToken).ConfigureAwait(false);
        int fingerprintCount = await repository.CountFingerprintsAsync(
            tombstone.Id,
            cancellationToken).ConfigureAwait(false);
        if (plan.Count != tombstone.GraphRecordCount ||
            plan.Count(entry => entry.RequiresRawPayloadDeletion) !=
                tombstone.RawPayloadCount ||
            fingerprintCount != tombstone.FingerprintCount)
        {
            return Conflict();
        }

        IngestionAnonymisationRestoreGraphLoadResult loaded =
            await repository.LoadPlannedGraphAsync(
                request.RoutingPropertyId,
                request.RecordId,
                plan,
                cancellationToken).ConfigureAwait(false);
        if (loaded.Graph is null)
        {
            return Conflict();
        }

        bool completed =
            tombstone.State ==
                IngestionAnonymisationTombstoneState.Completed;
        if (!IngestionAnonymisationStateVerifier.Matches(
                loaded.Graph,
                plan,
                tombstone.ReductionClaimId,
                completed))
        {
            return Conflict();
        }

        if (completed && tombstone.LastReplayedAtUtc.HasValue)
        {
            return Result.Success(CreateProof(tombstone));
        }

        foreach (IngestionAnonymisationRecordPlanEntry entry in plan
                     .Where(item => item.RequiresRawPayloadDeletion))
        {
            RawPayloadRead? raw = await rawPayloads.ReadAsync(
                entry.RawPayloadFileId!.Value,
                tenantId,
                entry.RawPayloadConnectionId!.Value,
                cancellationToken).ConfigureAwait(false);
            if (raw is not null)
            {
                return Result.Failure<
                    DataRightsAnonymisationRestoreProof>(
                        IngestionApplicationErrors
                            .AnonymisationRawPayloadDeletionIncomplete);
            }
        }

        DateTimeOffset replayedAtUtc =
            ToPersistencePrecision(clock.UtcNow);
        if (!completed)
        {
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
                    replayedAtUtc);
                if (purged.IsFailure)
                {
                    return Result.Failure<
                        DataRightsAnonymisationRestoreProof>(
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
        }

        Result finalized = tombstone.CompleteRestore(replayedAtUtc);
        if (finalized.IsFailure)
        {
            return Result.Failure<
                DataRightsAnonymisationRestoreProof>(finalized.Error);
        }

        return Result.Success(CreateProof(tombstone));
    }

    private static bool Matches(
        IngestionAnonymisationTombstone tombstone,
        DataRightsAnonymisationRestoreRequest request) =>
        tombstone.ResultingSourceLinkVersion ==
            request.ResultingRecordVersion &&
        tombstone.MatchesRestore(
            request.RoutingPropertyId,
            request.OwnerReceiptContractVersion,
            request.OwnerReceiptId,
            request.OwnerReceiptSha256,
            request.OriginallyCompletedAtUtc,
            request.LedgerEntryId,
            request.TenantSequence,
            request.LedgerEntrySha256);

    private static DataRightsAnonymisationRestoreProof CreateProof(
        IngestionAnonymisationTombstone tombstone) =>
        new(
            tombstone.LedgerEntryId,
            tombstone.OwnerReceiptId,
            tombstone.OwnerReceiptSha256,
            tombstone.ResultingSourceLinkVersion,
            tombstone.Revision,
            tombstone.LastReplayedAtUtc!.Value);

    private static Result<DataRightsAnonymisationRestoreProof>
        InvalidRequest() =>
        Result.Failure<DataRightsAnonymisationRestoreProof>(
            IngestionApplicationErrors.AnonymisationRestoreRequestInvalid);

    private static Result<DataRightsAnonymisationRestoreProof> Conflict() =>
        Result.Failure<DataRightsAnonymisationRestoreProof>(
            IngestionApplicationErrors.AnonymisationRestoreProofConflict);

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
