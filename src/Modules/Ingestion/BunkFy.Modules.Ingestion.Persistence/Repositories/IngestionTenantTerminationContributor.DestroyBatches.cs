namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Globalization;
using BunkFy.Modules.Ingestion.Persistence.TenantTermination;
using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal sealed partial class IngestionTenantTerminationContributor
{
    private Task<bool> RemoveCurrentDatabaseStageAsync(
        IngestionTenantDestroyOperation operation,
        string tenantId,
        CancellationToken cancellationToken) =>
        operation.Stage switch
        {
            IngestionTenantDestroyStage.OutboxMessages =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.OutboxMessages
                        .Where(message => message.ScopeId == tenantId)
                        .OrderBy(message => message.Id),
                    message => message.Id.ToString("N"),
                    cancellationToken),
            IngestionTenantDestroyStage.InboxMessages =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.InboxMessages
                        .Where(message => message.ScopeId == tenantId)
                        .OrderBy(message => message.Id)
                        .ThenBy(message => message.Handler),
                    message =>
                        $"{message.Id:N}|{LengthPrefixed(message.Handler)}",
                    cancellationToken),
            IngestionTenantDestroyStage.ReservationDispatches =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.ReservationDispatches,
                    dispatch => dispatch.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.ChangeProposals =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.ChangeProposals,
                    proposal => proposal.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.ObservationReprocessingOutputs =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.ObservationReprocessingOutputs,
                    output => output.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.ObservationReprocessingGraph =>
                this.RemoveReprocessingGraphBatchAsync(
                    operation,
                    cancellationToken),
            IngestionTenantDestroyStage.SourceObservationReceipts =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.ObservationReceipts
                        .Where(receipt => receipt.SourceReceiptId == null),
                    receipt => receipt.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.AnonymisationReceipts =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.AnonymisationReceipts,
                    receipt => receipt.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.AnonymisationFingerprints =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.AnonymisationFingerprints,
                    fingerprint => fingerprint.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.AnonymisationRecordPlan =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.AnonymisationRecordPlan,
                    entry => entry.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.AnonymisationTombstones =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.AnonymisationTombstones,
                    tombstone => tombstone.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.ReservationSourceLinks =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.ReservationSourceLinks,
                    link => link.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.Runs =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.Runs,
                    run => run.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.AdapterIngressCredentials =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.AdapterIngressCredentials,
                    credential => credential.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.LegalHolds =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.LegalHolds,
                    hold => hold.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.RetentionExecutions =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.RetentionExecutions,
                    execution => execution.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.AdapterConnections =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.AdapterConnections,
                    connection => connection.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.AdapterIngressTenantControls =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.AdapterIngressTenantControls
                        .OrderBy(control => control.Id),
                    control => LengthPrefixed(control.Id),
                    cancellationToken),
            IngestionTenantDestroyStage.PropertyProjections =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.PropertyProjections,
                    property => property.Id,
                    cancellationToken),
            IngestionTenantDestroyStage.ProjectionRebuildCheckpoints =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.ProjectionRebuildCheckpoints
                        .OrderBy(checkpoint => checkpoint.ProjectionName)
                        .ThenBy(checkpoint => checkpoint.RunId),
                    checkpoint =>
                        $"{LengthPrefixed(checkpoint.ProjectionName)}|" +
                        $"{checkpoint.RunId:N}",
                    cancellationToken),
            _ => throw new InvalidDataException(
                "The Ingestion tenant destruction stage is invalid.")
        };

    private Task<bool> RemoveGuidBatchAsync<TEntity>(
        IngestionTenantDestroyOperation operation,
        IQueryable<TEntity> source,
        System.Linq.Expressions.Expression<Func<TEntity, Guid>> idSelector,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        Func<TEntity, Guid> getId = idSelector.Compile();
        return this.RemoveBatchAsync(
            operation,
            source.OrderBy(idSelector),
            entity => getId(entity).ToString("N"),
            cancellationToken);
    }

    private async Task<bool> RemoveReprocessingGraphBatchAsync(
        IngestionTenantDestroyOperation operation,
        CancellationToken cancellationToken)
    {
        var derivedReceipts = await dbContext.ObservationReceipts
            .Where(receipt =>
                receipt.SourceReceiptId != null &&
                !dbContext.ObservationReceipts.Any(candidate =>
                    candidate.SourceReceiptId == receipt.Id) &&
                !dbContext.ObservationReprocessingAttempts.Any(attempt =>
                    attempt.SourceReceiptId == receipt.Id))
            .OrderBy(receipt => receipt.Id)
            .Take(operation.BatchSize + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (derivedReceipts.Length > 0)
        {
            var selected = derivedReceipts
                .Take(operation.BatchSize)
                .ToArray();
            dbContext.RemoveRange(selected);
            EnsureBatchRecorded(
                operation,
                selected.Select(receipt => $"receipt|{receipt.Id:N}")
                    .ToArray(),
                stageCompleted: false,
                clock.UtcNow);
            return true;
        }

        bool hasDerivedReceipts = await dbContext.ObservationReceipts
            .AnyAsync(
                receipt => receipt.SourceReceiptId != null,
                cancellationToken)
            .ConfigureAwait(false);
        var attempts = await dbContext.ObservationReprocessingAttempts
            .Where(attempt =>
                !dbContext.ObservationReceipts.Any(receipt =>
                    receipt.ReprocessingAttemptId == attempt.Id))
            .OrderBy(attempt => attempt.Id)
            .Take(operation.BatchSize + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (attempts.Length > 0)
        {
            var selected = attempts.Take(operation.BatchSize).ToArray();
            dbContext.RemoveRange(selected);
            EnsureBatchRecorded(
                operation,
                selected.Select(attempt => $"attempt|{attempt.Id:N}")
                    .ToArray(),
                stageCompleted:
                    !hasDerivedReceipts &&
                    attempts.Length <= operation.BatchSize,
                clock.UtcNow);
            return true;
        }

        bool hasAttempts = await dbContext.ObservationReprocessingAttempts
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (hasDerivedReceipts || hasAttempts)
        {
            throw new InvalidDataException(
                "The Ingestion reprocessing graph is cyclic or inconsistent.");
        }

        EnsureStageAdvanced(operation, clock.UtcNow);
        return false;
    }

    private async Task<bool> RemoveBatchAsync<TEntity>(
        IngestionTenantDestroyOperation operation,
        IQueryable<TEntity> source,
        Func<TEntity, string> keySelector,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        TEntity[] loaded = await source
            .Take(operation.BatchSize + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Length == 0)
        {
            EnsureStageAdvanced(operation, clock.UtcNow);
            return false;
        }

        TEntity[] selected = loaded.Take(operation.BatchSize).ToArray();
        string[] keys = selected.Select(keySelector).ToArray();
        dbContext.RemoveRange(selected);
        EnsureBatchRecorded(
            operation,
            keys,
            loaded.Length <= operation.BatchSize,
            clock.UtcNow);
        return true;
    }

    private async Task<bool> HasRemainingOwnerRecordsAsync(
        string tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.OutboxMessages.AnyAsync(
            message => message.ScopeId == tenantId,
            cancellationToken).ConfigureAwait(false) ||
        await dbContext.InboxMessages.AnyAsync(
            message => message.ScopeId == tenantId,
            cancellationToken).ConfigureAwait(false) ||
        await dbContext.ReservationDispatches.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.ChangeProposals.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.ObservationReprocessingOutputs
            .AnyAsync(cancellationToken).ConfigureAwait(false) ||
        await dbContext.ObservationReprocessingAttempts
            .AnyAsync(cancellationToken).ConfigureAwait(false) ||
        await dbContext.ObservationReceipts.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.AnonymisationReceipts.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.AnonymisationFingerprints.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.AnonymisationRecordPlan.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.AnonymisationTombstones.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.ReservationSourceLinks.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.Runs.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.AdapterIngressCredentials.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.LegalHolds.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.RetentionExecutions.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.AdapterConnections.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.AdapterIngressTenantControls.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.PropertyProjections.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.ProjectionRebuildCheckpoints
            .AnyAsync(cancellationToken).ConfigureAwait(false);

    private static void EnsureBatchRecorded(
        IngestionTenantDestroyOperation operation,
        string[] keys,
        bool stageCompleted,
        DateTimeOffset recordedAtUtc)
    {
        IngestionTenantDestroyStage stage = operation.Stage;
        string canonicalKeys = string.Concat(keys.Select(LengthPrefixed));
        string keysSha256 = IngestionTenantLifecycleHashes.Sha256(
            "bunkfy-ingestion-tenant-destroy-keys/v1|" +
            $"{(int)stage}|{keys.Length.ToString(CultureInfo.InvariantCulture)}|" +
            canonicalKeys);
        if (!operation.RecordBatch(
                stage,
                keys.Length,
                keysSha256,
                stageCompleted,
                recordedAtUtc))
        {
            throw new InvalidDataException(
                "Ingestion tenant destruction batch progress is invalid.");
        }
    }

    private static void EnsureStageAdvanced(
        IngestionTenantDestroyOperation operation,
        DateTimeOffset recordedAtUtc)
    {
        if (!operation.AdvanceEmptyStage(recordedAtUtc))
        {
            throw new InvalidDataException(
                "Ingestion tenant destruction stage progress is invalid.");
        }
    }

    private static string LengthPrefixed(string value) =>
        $"{value.Length.ToString(CultureInfo.InvariantCulture)}:{value}";
}
