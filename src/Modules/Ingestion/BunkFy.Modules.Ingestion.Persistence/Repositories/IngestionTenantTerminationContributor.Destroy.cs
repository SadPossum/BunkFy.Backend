namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Persistence.TenantTermination;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal sealed partial class IngestionTenantTerminationContributor
{
    private const int DestroyBatchSize =
        IngestionTenantDestroyOperation.MaximumBatchSize;
    private const int RawPayloadDestroyBatchSize = 32;
    private const int RawPayloadDeleteConcurrency = 8;

    private async Task<TenantTerminationContributionResult> DestroyAsync(
        TenantTerminationContributionRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAtUtc = clock.UtcNow;
        if (!IsValidDestroy(request, scopeContext, startedAtUtc) ||
            !TenantIds.TryNormalize(request.TenantId, out string? tenantId))
        {
            return Failed(
                "ingestion.termination.destroy-request-invalid",
                startedAtUtc);
        }

        if (rawPayloads is null)
        {
            return Failed(
                "ingestion.termination.destroy-raw-payload-store-unavailable",
                startedAtUtc);
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is not null)
        {
            return Failed(
                "ingestion.termination.destroy-transaction-conflict",
                startedAtUtc);
        }

        string requestSha256 = DestroyRequestSha256(request, tenantId);
        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await this.BeginDestroyTransactionAsync(
                    tenantId,
                    request.IdempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);

            WorkspaceTerminationFenceSnapshot? fence =
                await this.ReadFenceAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (!Matches(request, fence))
            {
                return await FinishAsync(
                    transaction,
                    RetryRequired(
                        "ingestion.termination.destroy-fence-unavailable",
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            IngestionTenantDestroyReceipt[] receiptMatches =
                await dbContext.TenantDestroyReceipts
                    .Where(receipt =>
                        receipt.OperationId == request.IdempotencyKey ||
                        receipt.ScopeId == tenantId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (receiptMatches.Length > 0)
            {
                IngestionTenantDestroyReceipt? exact = receiptMatches
                    .SingleOrDefault(receipt => receipt.ScopeId == tenantId);
                TenantTerminationContributionResult replay =
                    exact is not null &&
                    receiptMatches.Length == 1 &&
                    exact.Matches(request.IdempotencyKey, requestSha256)
                        ? Completed(exact)
                        : Failed(
                            "ingestion.termination.destroy-conflict",
                            clock.UtcNow);
                return await FinishAsync(
                    transaction,
                    replay,
                    cancellationToken).ConfigureAwait(false);
            }

            IngestionTenantDestroyOperation[] operationMatches =
                await dbContext.TenantDestroyOperations
                    .Where(operation =>
                        operation.OperationId == request.IdempotencyKey ||
                        operation.ScopeId == tenantId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
            IngestionTenantDestroyOperation? operation = operationMatches
                .SingleOrDefault(candidate => candidate.ScopeId == tenantId);
            if (operationMatches.Length > 0 &&
                (operation is null ||
                 operationMatches.Length != 1 ||
                 !operation.Matches(request.IdempotencyKey, requestSha256)))
            {
                return await FinishAsync(
                    transaction,
                    Failed(
                        "ingestion.termination.destroy-conflict",
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            IngestionTenantRevision? state = await dbContext.TenantRevisions
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (operation is null)
            {
                if (state is not null && !state.IsOpen)
                {
                    return await FinishAsync(
                        transaction,
                        Failed(
                            "ingestion.termination.destroy-conflict",
                            clock.UtcNow),
                        cancellationToken).ConfigureAwait(false);
                }

                long activeHoldCount = await dbContext.LegalHolds.LongCountAsync(
                        hold => hold.State == LegalHoldState.Active,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (activeHoldCount > 0)
                {
                    return await FinishAsync(
                        transaction,
                        Blocked(
                            "ingestion.termination.destroy-active-hold",
                            activeHoldCount,
                            clock.UtcNow),
                        cancellationToken).ConfigureAwait(false);
                }

                long selectedRevision = state?.Revision ?? 0;
                DateTimeOffset operationStartedAtUtc = clock.UtcNow;
                operation = IngestionTenantDestroyOperation.TryCreate(
                    request.IdempotencyKey,
                    tenantId,
                    requestSha256,
                    selectedRevision,
                    DestroyBatchSize,
                    operationStartedAtUtc);
                if (state is null)
                {
                    state = IngestionTenantRevision.TryBeginClosing(
                        tenantId,
                        request.IdempotencyKey,
                        requestSha256,
                        operationStartedAtUtc);
                    if (state is not null)
                    {
                        dbContext.TenantRevisions.Add(state);
                    }
                }
                else if (!state.BeginClosing(
                    request.IdempotencyKey,
                    requestSha256,
                    selectedRevision,
                    operationStartedAtUtc))
                {
                    state = null;
                }

                if (operation is null || state is null)
                {
                    return await FinishAsync(
                        transaction,
                        Failed(
                            "ingestion.termination.destroy-state-invalid",
                            clock.UtcNow),
                        cancellationToken).ConfigureAwait(false);
                }

                dbContext.TenantDestroyOperations.Add(operation);
                await dbContext.SaveTenantDestructionChangesAsync(
                        tenantId,
                        request.IdempotencyKey,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (state is null ||
                state.LifecycleStatus != IngestionTenantLifecycleStatus.Closing ||
                !state.Matches(request.IdempotencyKey, requestSha256) ||
                state.Revision != operation.ResultingRevision)
            {
                return await FinishAsync(
                    transaction,
                    Failed(
                        "ingestion.termination.destroy-conflict",
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            DateTimeOffset observedAtUtc = clock.UtcNow;
            bool hasActiveOutboxLease = await dbContext.OutboxMessages
                .AnyAsync(
                    message =>
                        message.ScopeId == tenantId &&
                        message.ProcessedAtUtc == null &&
                        message.LockedBy != null &&
                        message.LockedUntilUtc > observedAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            if (hasActiveOutboxLease)
            {
                return await FinishAsync(
                    transaction,
                    RetryRequired(
                        "ingestion.termination.destroy-outbox-busy",
                        Affected(operation),
                        observedAtUtc),
                    cancellationToken).ConfigureAwait(false);
            }

            while (!operation.IsComplete)
            {
                if (operation.Stage ==
                    IngestionTenantDestroyStage.RawPayloadObjects)
                {
                    IngestionRawPayloadDestroyCandidate[] candidates =
                        await this.SelectRawPayloadBatchAsync(
                            cancellationToken).ConfigureAwait(false);
                    if (candidates.Length == 0)
                    {
                        EnsureStageAdvanced(operation, clock.UtcNow);
                        await dbContext.SaveTenantDestructionChangesAsync(
                                tenantId,
                                request.IdempotencyKey,
                                cancellationToken)
                            .ConfigureAwait(false);
                        continue;
                    }

                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(cancellationToken)
                            .ConfigureAwait(false);
                        await transaction.DisposeAsync().ConfigureAwait(false);
                        transaction = null;
                    }

                    dbContext.ChangeTracker.Clear();
                    bool deleted = await this.DeleteAndVerifyRawPayloadsAsync(
                            tenantId,
                            candidates,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!deleted)
                    {
                        return RetryRequired(
                            "ingestion.termination.destroy-raw-payload-retry",
                            Affected(operation),
                            clock.UtcNow);
                    }

                    return await this.RecordRawPayloadBatchAsync(
                            request,
                            tenantId,
                            requestSha256,
                            candidates,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                bool removed = await this.RemoveCurrentDatabaseStageAsync(
                        operation,
                        tenantId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!removed)
                {
                    continue;
                }

                await dbContext.SaveTenantDestructionChangesAsync(
                        tenantId,
                        request.IdempotencyKey,
                        cancellationToken)
                    .ConfigureAwait(false);
                return await FinishAsync(
                    transaction,
                    RetryRequired(
                        "ingestion.termination.destroy-in-progress",
                        Affected(operation),
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            if (await this.HasRemainingOwnerRecordsAsync(
                    tenantId,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException(
                    "Ingestion tenant destruction completed with remaining owner records.");
            }

            DateTimeOffset completedAtUtc = clock.UtcNow;
            if (completedAtUtc > request.DeadlineUtc)
            {
                return await FinishAsync(
                    transaction,
                    RetryRequired(
                        "ingestion.termination.destroy-deadline-expired",
                        Affected(operation),
                        completedAtUtc),
                    cancellationToken).ConfigureAwait(false);
            }

            IngestionTenantDestroyReceipt? receipt =
                IngestionTenantDestroyReceipt.TryCreate(
                    operation,
                    completedAtUtc);
            if (receipt is null || !state.CompleteDestruction(completedAtUtc))
            {
                throw new InvalidDataException(
                    "Ingestion tenant destruction completion proof is invalid.");
            }

            dbContext.TenantDestroyReceipts.Add(receipt);
            dbContext.TenantDestroyOperations.Remove(operation);
            await dbContext.SaveTenantDestructionChangesAsync(
                    tenantId,
                    request.IdempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);
            return await FinishAsync(
                transaction,
                Completed(receipt),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<IDbContextTransaction?> BeginDestroyTransactionAsync(
        string tenantId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return null;
        }

        IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken)
                .ConfigureAwait(false);
        await IngestionTenantMutationLock.AcquireLifecycleAsync(
                dbContext,
                tenantId,
                operationId,
                cancellationToken)
            .ConfigureAwait(false);
        return transaction;
    }

    private async Task<IngestionRawPayloadDestroyCandidate[]>
        SelectRawPayloadBatchAsync(CancellationToken cancellationToken) =>
        await dbContext.ObservationReceipts
            .AsNoTracking()
            .Where(receipt => receipt.RawPayloadRetentionState !=
                RawPayloadRetentionState.Purged)
            .OrderBy(receipt => receipt.Id)
            .Take(RawPayloadDestroyBatchSize)
            .Select(receipt => new IngestionRawPayloadDestroyCandidate(
                receipt.Id,
                receipt.RawPayloadFileId,
                receipt.ConnectionId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<bool> DeleteAndVerifyRawPayloadsAsync(
        string tenantId,
        IReadOnlyCollection<IngestionRawPayloadDestroyCandidate> candidates,
        CancellationToken cancellationToken)
    {
        IRawPayloadStore payloadStore = rawPayloads ??
            throw new InvalidOperationException(
                "Ingestion raw-payload storage is unavailable.");
        ConcurrentQueue<Guid> incomplete = new();
        try
        {
            await Parallel.ForEachAsync(
                candidates,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = RawPayloadDeleteConcurrency
                },
                async (candidate, itemCancellationToken) =>
                {
                    _ = await payloadStore.DeleteAsync(
                        candidate.RawPayloadFileId,
                        tenantId,
                        candidate.ConnectionId,
                        itemCancellationToken).ConfigureAwait(false);
                    RawPayloadRead? remaining = await payloadStore.ReadAsync(
                        candidate.RawPayloadFileId,
                        tenantId,
                        candidate.ConnectionId,
                        itemCancellationToken).ConfigureAwait(false);
                    if (remaining is not null)
                    {
                        incomplete.Enqueue(candidate.ReceiptId);
                    }
                }).ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            return false;
        }

        return incomplete.IsEmpty;
    }

    private async Task<TenantTerminationContributionResult>
        RecordRawPayloadBatchAsync(
            TenantTerminationContributionRequest request,
            string tenantId,
            string requestSha256,
            IngestionRawPayloadDestroyCandidate[] candidates,
            CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await this.BeginDestroyTransactionAsync(
                    tenantId,
                    request.IdempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);

            WorkspaceTerminationFenceSnapshot? fence =
                await this.ReadFenceAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (!Matches(request, fence))
            {
                return await FinishAsync(
                    transaction,
                    RetryRequired(
                        "ingestion.termination.destroy-fence-unavailable",
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            IngestionTenantDestroyOperation? operation =
                await dbContext.TenantDestroyOperations
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            IngestionTenantRevision? state = await dbContext.TenantRevisions
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (operation is null || state is null ||
                !operation.Matches(request.IdempotencyKey, requestSha256) ||
                !state.Matches(request.IdempotencyKey, requestSha256) ||
                state.LifecycleStatus !=
                    IngestionTenantLifecycleStatus.Closing)
            {
                return await FinishAsync(
                    transaction,
                    Failed(
                        "ingestion.termination.destroy-conflict",
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            if (operation.Stage !=
                IngestionTenantDestroyStage.RawPayloadObjects)
            {
                return await FinishAsync(
                    transaction,
                    RetryRequired(
                        "ingestion.termination.destroy-in-progress",
                        Affected(operation),
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            Guid[] receiptIds = candidates
                .Select(candidate => candidate.ReceiptId)
                .ToArray();
            ObservationReceipt[] receipts = await dbContext.ObservationReceipts
                .Where(receipt => receiptIds.Contains(receipt.Id))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            if (receipts.Length != candidates.Length)
            {
                throw new InvalidDataException(
                    "An Ingestion raw-payload destruction candidate disappeared.");
            }

            Dictionary<Guid, IngestionRawPayloadDestroyCandidate> expected =
                candidates.ToDictionary(candidate => candidate.ReceiptId);
            if (receipts.Any(receipt =>
                    !expected.TryGetValue(receipt.Id, out var candidate) ||
                    candidate.RawPayloadFileId != receipt.RawPayloadFileId ||
                    candidate.ConnectionId != receipt.ConnectionId))
            {
                throw new InvalidDataException(
                    "An Ingestion raw-payload destruction coordinate changed.");
            }

            if (receipts.Any(receipt => receipt.RawPayloadRetentionState ==
                RawPayloadRetentionState.Purged))
            {
                return await FinishAsync(
                    transaction,
                    RetryRequired(
                        "ingestion.termination.destroy-in-progress",
                        Affected(operation),
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            DateTimeOffset recordedAtUtc = clock.UtcNow;
            foreach (ObservationReceipt receipt in receipts)
            {
                if (receipt.CompleteTenantDestructionRawPayloadPurge(
                        request.IdempotencyKey,
                        recordedAtUtc).IsFailure)
                {
                    throw new InvalidDataException(
                        "An Ingestion raw payload could not record destruction.");
                }
            }

            string[] keys = candidates
                .OrderBy(candidate => candidate.ReceiptId)
                .Select(candidate =>
                    $"{candidate.ReceiptId:N}|" +
                    $"{candidate.RawPayloadFileId:N}|" +
                    $"{candidate.ConnectionId:N}")
                .ToArray();
            string keysSha256 = IngestionTenantLifecycleHashes.Sha256(
                "bunkfy-ingestion-tenant-destroy-raw-payload-keys/v1|" +
                $"{keys.Length.ToString(CultureInfo.InvariantCulture)}|" +
                string.Concat(keys.Select(LengthPrefixed)));
            if (!operation.RecordRawPayloadBatch(
                    keys.Length,
                    keysSha256,
                    candidates.Length < RawPayloadDestroyBatchSize,
                    recordedAtUtc))
            {
                throw new InvalidDataException(
                    "Ingestion raw-payload destruction progress is invalid.");
            }

            await dbContext.SaveTenantDestructionChangesAsync(
                    tenantId,
                    request.IdempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);
            string code = recordedAtUtc > request.DeadlineUtc
                ? "ingestion.termination.destroy-deadline-expired"
                : "ingestion.termination.destroy-in-progress";
            return await FinishAsync(
                transaction,
                RetryRequired(code, Affected(operation), recordedAtUtc),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static bool IsValidDestroy(
        TenantTerminationContributionRequest request,
        Gma.Framework.Scoping.IScopeContext scope,
        DateTimeOffset nowUtc) =>
        request is not null &&
        request.ContractVersion == TenantTerminationContract.CurrentVersion &&
        TenantIds.TryNormalize(request.TenantId, out string? tenantId) &&
        scope.IsEnabled &&
        string.Equals(scope.ScopeId, tenantId, StringComparison.Ordinal) &&
        request.ProcessId != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        request.TerminationEpoch != Guid.Empty &&
        request.Phase == TenantTerminationContributionPhase.Destroy &&
        request.WorkItemId != Guid.Empty &&
        request.IdempotencyKey != Guid.Empty &&
        IsSha256(request.PolicyEvidenceSha256) &&
        request.DeadlineUtc > nowUtc &&
        IsActor(request.ExecutingActorId);

    private static bool Matches(
        TenantTerminationContributionRequest request,
        WorkspaceTerminationFenceSnapshot? fence) =>
        fence is not null &&
        fence.ProcessId == request.ProcessId &&
        fence.TerminationEpoch == request.TerminationEpoch &&
        fence.State == WorkspaceTerminationFenceState.Frozen;

    private static string DestroyRequestSha256(
        TenantTerminationContributionRequest request,
        string tenantId)
    {
        static string Field(string value) =>
            $"{value.Length.ToString(CultureInfo.InvariantCulture)}:{value}";

        return IngestionTenantLifecycleHashes.Sha256(
            "bunkfy-ingestion-tenant-destroy-request/v1|" +
            $"{request.IdempotencyKey:N}|{Field(tenantId)}|" +
            $"{request.ProcessId:N}|{request.CaseId:N}|" +
            $"{request.ApprovalRevision.ToString(CultureInfo.InvariantCulture)}|" +
            $"{request.OperationRevision.ToString(CultureInfo.InvariantCulture)}|" +
            $"{request.TerminationEpoch:N}|{request.WorkItemId:N}|" +
            $"{request.PolicyEvidenceSha256}|" +
            $"{Field(request.ExecutingActorId)}|{DestroyBatchSize}|" +
            $"{RawPayloadDestroyBatchSize}|{RawPayloadDeleteConcurrency}");
    }

    private static long Affected(
        IngestionTenantDestroyOperation operation) =>
        checked(operation.RemovedRecordCount +
            operation.RemovedRawPayloadCount);

    private static TenantTerminationContributionResult Completed(
        IngestionTenantDestroyReceipt receipt) =>
        new(
            TenantTerminationContributionStatus.Completed,
            "ingestion.termination.destroyed",
            receipt.RemovedArtifactCount,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 0,
            HoldReviewAtUtc: null,
            receipt.SelectedRevision,
            receipt.ResultingRevision,
            IngestionTenantTerminationMetadata.CatalogVersion,
            IngestionTenantTerminationMetadata.CatalogSha256,
            receipt.CompletedAtUtc);

    private static TenantTerminationContributionResult Blocked(
        string code,
        long remainingActiveCount,
        DateTimeOffset recordedAtUtc) =>
        new(
            TenantTerminationContributionStatus.Blocked,
            code,
            AffectedCount: 0,
            RetainedMinimumCount: 0,
            Math.Max(1, remainingActiveCount),
            HoldReviewAtUtc: null,
            SelectedProofRevision: null,
            ResultingProofRevision: null,
            IngestionTenantTerminationMetadata.CatalogVersion,
            IngestionTenantTerminationMetadata.CatalogSha256,
            recordedAtUtc);

    private static TenantTerminationContributionResult RetryRequired(
        string code,
        long affectedCount,
        DateTimeOffset recordedAtUtc) =>
        new(
            TenantTerminationContributionStatus.RetryRequired,
            code,
            affectedCount,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 1,
            HoldReviewAtUtc: null,
            SelectedProofRevision: null,
            ResultingProofRevision: null,
            IngestionTenantTerminationMetadata.CatalogVersion,
            IngestionTenantTerminationMetadata.CatalogSha256,
            recordedAtUtc);

    private static async Task<TenantTerminationContributionResult> FinishAsync(
        IDbContextTransaction? transaction,
        TenantTerminationContributionResult result,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    private sealed record IngestionRawPayloadDestroyCandidate(
        Guid ReceiptId,
        Guid RawPayloadFileId,
        Guid ConnectionId);
}
