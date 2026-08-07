namespace BunkFy.Modules.Guests.Persistence.Repositories;

using System.Globalization;
using BunkFy.Modules.Guests.Persistence.TenantTermination;
using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;

internal sealed partial class GuestsTenantTerminationContributor
{
    private Task<bool> RemoveCurrentStageAsync(
        GuestsTenantDestroyOperation operation,
        string tenantId,
        CancellationToken cancellationToken) =>
        operation.Stage switch
        {
            GuestsTenantDestroyStage.OutboxMessages =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.OutboxMessages
                        .Where(message => message.ScopeId == tenantId)
                        .OrderBy(message => message.Id),
                    message => message.Id.ToString("N"),
                    cancellationToken),
            GuestsTenantDestroyStage.InboxMessages =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.InboxMessages
                        .Where(message => message.ScopeId == tenantId)
                        .OrderBy(message => message.Id)
                        .ThenBy(message => message.Handler),
                    message =>
                        $"{message.Id:N}|{LengthPrefixed(message.Handler)}",
                    cancellationToken),
            GuestsTenantDestroyStage.RetentionAnonymisationReceipts =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.RetentionAnonymisationReceipts,
                    receipt => receipt.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.AnonymisationReceipts =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.AnonymisationReceipts,
                    receipt => receipt.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.AnonymisationRestoreReceipts =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.AnonymisationRestoreReceipts,
                    receipt => receipt.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.AnonymisationTombstones =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.AnonymisationTombstones,
                    tombstone => tombstone.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.DataHoldReceipts =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.DataHoldReceipts,
                    receipt => receipt.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.ProcessingRestrictionReceipts =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.ProcessingRestrictionReceipts,
                    receipt => receipt.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.DataRightsCorrectionReceipts =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.DataRightsCorrectionReceipts,
                    receipt => receipt.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.DataHolds =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.DataHolds,
                    hold => hold.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.ProcessingRestrictions =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.ProcessingRestrictions,
                    restriction => restriction.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.RetentionExecutions =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.RetentionExecutions,
                    execution => execution.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.StayHistory =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.StayHistory
                        .OrderBy(stay => stay.GuestId)
                        .ThenBy(stay => stay.ReservationId),
                    stay => $"{stay.GuestId:N}|{stay.ReservationId:N}",
                    cancellationToken),
            GuestsTenantDestroyStage.ManagementOperations =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.ManagementOperations
                        .OrderBy(item => item.GuestId)
                        .ThenBy(item => item.Id),
                    item => $"{item.GuestId:N}|{item.Id:N}",
                    cancellationToken),
            GuestsTenantDestroyStage.GuestProfiles =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.GuestProfiles,
                    profile => profile.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.ProcessingRestrictionProjections =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.ProcessingRestrictionProjections
                        .OrderBy(projection => projection.PropertyId)
                        .ThenBy(projection => projection.GuestId),
                    projection =>
                        $"{projection.PropertyId:N}|{projection.GuestId:N}",
                    cancellationToken),
            GuestsTenantDestroyStage.PropertyProjections =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.PropertyProjections,
                    property => property.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.ProjectionRebuildCheckpoints =>
                this.RemoveBatchAsync(
                    operation,
                    dbContext.ProjectionRebuildCheckpoints
                        .OrderBy(checkpoint => checkpoint.ProjectionName)
                        .ThenBy(checkpoint => checkpoint.RunId),
                    checkpoint =>
                        $"{LengthPrefixed(checkpoint.ProjectionName)}|" +
                        $"{checkpoint.RunId:N}",
                    cancellationToken),
            GuestsTenantDestroyStage.RetentionSweepCheckpoints =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.RetentionSweepCheckpoints,
                    checkpoint => checkpoint.Id,
                    cancellationToken),
            GuestsTenantDestroyStage.OperationLocks =>
                this.RemoveGuidBatchAsync(
                    operation,
                    dbContext.OperationLocks,
                    resourceLock => resourceLock.Id,
                    cancellationToken),
            _ => throw new InvalidDataException(
                "The Guests tenant destruction stage is invalid.")
        };

    private Task<bool> RemoveGuidBatchAsync<TEntity>(
        GuestsTenantDestroyOperation operation,
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

    private async Task<bool> RemoveBatchAsync<TEntity>(
        GuestsTenantDestroyOperation operation,
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
        await dbContext.RetentionAnonymisationReceipts
            .AnyAsync(cancellationToken).ConfigureAwait(false) ||
        await dbContext.AnonymisationReceipts.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.AnonymisationRestoreReceipts
            .AnyAsync(cancellationToken).ConfigureAwait(false) ||
        await dbContext.AnonymisationTombstones.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.DataHoldReceipts.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.ProcessingRestrictionReceipts
            .AnyAsync(cancellationToken).ConfigureAwait(false) ||
        await dbContext.DataRightsCorrectionReceipts
            .AnyAsync(cancellationToken).ConfigureAwait(false) ||
        await dbContext.DataHolds.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.ProcessingRestrictions.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.RetentionExecutions.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.StayHistory.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.ManagementOperations.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.GuestProfiles.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.ProcessingRestrictionProjections
            .AnyAsync(cancellationToken).ConfigureAwait(false) ||
        await dbContext.PropertyProjections.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.ProjectionRebuildCheckpoints
            .AnyAsync(cancellationToken).ConfigureAwait(false) ||
        await dbContext.RetentionSweepCheckpoints.AnyAsync(cancellationToken)
            .ConfigureAwait(false) ||
        await dbContext.OperationLocks.AnyAsync(cancellationToken)
            .ConfigureAwait(false);

    private static void EnsureBatchRecorded(
        GuestsTenantDestroyOperation operation,
        string[] keys,
        bool stageCompleted,
        DateTimeOffset recordedAtUtc)
    {
        GuestsTenantDestroyStage stage = operation.Stage;
        string canonicalKeys = string.Concat(keys.Select(LengthPrefixed));
        string keysSha256 = GuestsTenantLifecycleHashes.Sha256(
            "bunkfy-guests-tenant-destroy-keys/v1|" +
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
                "Guests tenant destruction batch progress is invalid.");
        }
    }

    private static void EnsureStageAdvanced(
        GuestsTenantDestroyOperation operation,
        DateTimeOffset recordedAtUtc)
    {
        if (!operation.AdvanceEmptyStage(recordedAtUtc))
        {
            throw new InvalidDataException(
                "Guests tenant destruction stage progress is invalid.");
        }
    }

    private static string LengthPrefixed(string value) =>
        $"{value.Length.ToString(CultureInfo.InvariantCulture)}:{value}";
}
