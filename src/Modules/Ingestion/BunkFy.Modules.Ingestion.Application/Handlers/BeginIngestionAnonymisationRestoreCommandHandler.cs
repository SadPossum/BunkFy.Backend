namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.DataRights;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class BeginIngestionAnonymisationRestoreCommandHandler(
    IIngestionAnonymisationRestoreRepository repository,
    IIngestionSourceOperationLock operationLock,
    IIngestionAnonymisationFingerprintService fingerprintService,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        BeginIngestionAnonymisationRestoreCommand,
        IngestionAnonymisationRestoreStage>
{
    public async Task<Result<IngestionAnonymisationRestoreStage>>
        HandleAsync(
            BeginIngestionAnonymisationRestoreCommand command,
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

        IngestionAnonymisationTombstone? existing =
            await repository.GetTombstoneAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await this.ResumeAsync(
                existing,
                request,
                cancellationToken).ConfigureAwait(false);
        }

        IngestionAnonymisationRestoreGraphLoadResult loaded =
            await repository.LoadInitialGraphAsync(
                request.RoutingPropertyId,
                request.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (loaded is not
            {
                Status: IngestionAnonymisationRestoreGraphLoadStatus.Found,
                Graph: { } graph
            })
        {
            return Unavailable();
        }

        long selectedSourceLinkVersion =
            request.ResultingRecordVersion!.Value - 1;
        if (graph.SourceLink.Version != selectedSourceLinkVersion)
        {
            return Conflict();
        }

        DateTimeOffset replayStartedAtUtc =
            ToPersistencePrecision(clock.UtcNow);
        Result<IReadOnlyList<IngestionAnonymisationFingerprintValue>>
            fingerprintValues =
                IngestionAnonymisationRestoreFingerprintFactory.CreateValues(
                    fingerprintService,
                    tenantId,
                    graph);
        if (fingerprintValues.IsFailure)
        {
            return Result.Failure<IngestionAnonymisationRestoreStage>(
                fingerprintValues.Error);
        }

        Result<IReadOnlyList<IngestionAnonymisationRecordPlanEntry>>
            planned = IngestionAnonymisationRestorePlanBuilder.Create(
                ids,
                tenantId,
                graph,
                replayStartedAtUtc);
        if (planned.IsFailure)
        {
            return Result.Failure<IngestionAnonymisationRestoreStage>(
                planned.Error);
        }

        Result<IngestionAnonymisationTombstone> tombstone =
            IngestionAnonymisationTombstone.BeginRestore(
                tenantId,
                request.RecordId,
                request.RoutingPropertyId,
                graph.SourceLink.ConnectionId,
                selectedSourceLinkVersion,
                request.ResultingRecordVersion.Value,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.OriginallyCompletedAtUtc,
                request.LedgerEntryId,
                request.TenantSequence,
                request.LedgerEntrySha256,
                graph.RecordCount,
                fingerprintValues.Value.Count,
                planned.Value.Count(entry =>
                    entry.RequiresRawPayloadDeletion),
                replayStartedAtUtc);
        if (tombstone.IsFailure)
        {
            return Result.Failure<IngestionAnonymisationRestoreStage>(
                tombstone.Error);
        }

        Result<IReadOnlyList<IngestionAnonymisationFingerprint>>
            fingerprints =
                IngestionAnonymisationRestoreFingerprintFactory
                    .CreateEntities(
                        ids,
                        tenantId,
                        request.RecordId,
                        fingerprintValues.Value,
                        replayStartedAtUtc);
        if (fingerprints.IsFailure)
        {
            return Result.Failure<IngestionAnonymisationRestoreStage>(
                fingerprints.Error);
        }

        Result reduced = IngestionAnonymisationRestoreGraphReducer.Reduce(
            graph,
            request.LedgerEntryId,
            selectedSourceLinkVersion,
            replayStartedAtUtc);
        if (reduced.IsFailure)
        {
            return Result.Failure<IngestionAnonymisationRestoreStage>(
                reduced.Error);
        }

        if (!IngestionAnonymisationRestoreStateVerifier.Matches(
                graph,
                planned.Value,
                request.LedgerEntryId,
                completed: false))
        {
            return Conflict();
        }

        repository.AddRestoreState(
            tombstone.Value,
            fingerprints.Value,
            planned.Value);
        return Result.Success(ToStage(tombstone.Value, planned.Value));
    }

    private async Task<Result<IngestionAnonymisationRestoreStage>>
        ResumeAsync(
            IngestionAnonymisationTombstone tombstone,
            DataRightsAnonymisationRestoreRequest request,
            CancellationToken cancellationToken)
    {
        if (!Matches(tombstone, request))
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
        bool completed =
            tombstone.State ==
                IngestionAnonymisationTombstoneState.Completed;
        if (loaded.Graph is null ||
            !IngestionAnonymisationRestoreStateVerifier.Matches(
                loaded.Graph,
                plan,
                request.LedgerEntryId,
                completed))
        {
            return Conflict();
        }

        return Result.Success(ToStage(tombstone, plan));
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

    private static IngestionAnonymisationRestoreStage ToStage(
        IngestionAnonymisationTombstone tombstone,
        IEnumerable<IngestionAnonymisationRecordPlanEntry> plan) =>
        new(
            tombstone.Id,
            plan.Where(entry => entry.RequiresRawPayloadDeletion)
                .OrderBy(entry => entry.RawPayloadConnectionId)
                .ThenBy(entry => entry.RawPayloadFileId)
                .Select(entry => new IngestionRawPayloadDeletion(
                    entry.RawPayloadFileId!.Value,
                    entry.RawPayloadConnectionId!.Value))
                .ToArray());

    private static Result<IngestionAnonymisationRestoreStage>
        InvalidRequest() =>
        Result.Failure<IngestionAnonymisationRestoreStage>(
            IngestionApplicationErrors.AnonymisationRestoreRequestInvalid);

    private static Result<IngestionAnonymisationRestoreStage> Conflict() =>
        Result.Failure<IngestionAnonymisationRestoreStage>(
            IngestionApplicationErrors.AnonymisationRestoreProofConflict);

    private static Result<IngestionAnonymisationRestoreStage> Unavailable() =>
        Result.Failure<IngestionAnonymisationRestoreStage>(
            IngestionApplicationErrors.AnonymisationRestoreStateUnavailable);

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
