namespace BunkFy.Modules.Guests.Persistence.Repositories;

using System.Data;
using System.Globalization;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Models;
using BunkFy.Modules.Guests.Persistence.TenantTermination;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal sealed partial class GuestsTenantTerminationContributor
{
    private const int DestroyBatchSize =
        GuestsTenantDestroyOperation.MaximumBatchSize;

    private async Task<TenantTerminationContributionResult> DestroyAsync(
        TenantTerminationContributionRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAtUtc = clock.UtcNow;
        if (!IsValidDestroy(request, scopeContext, startedAtUtc) ||
            !TenantIds.TryNormalize(request.TenantId, out string? tenantId))
        {
            return Failed(
                "guests.termination.destroy-request-invalid",
                startedAtUtc);
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is not null)
        {
            return Failed(
                "guests.termination.destroy-transaction-conflict",
                startedAtUtc);
        }

        string requestSha256 = DestroyRequestSha256(request, tenantId);
        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken)
                    .ConfigureAwait(false);
                await GuestsTenantMutationLock.AcquireLifecycleAsync(
                        dbContext,
                        tenantId,
                        request.IdempotencyKey,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            WorkspaceTerminationFenceSnapshot? fence =
                await this.ReadFenceAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (!Matches(request, fence))
            {
                return await FinishAsync(
                    transaction,
                    RetryRequired(
                        "guests.termination.destroy-fence-unavailable",
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            GuestsTenantDestroyReceipt[] receiptMatches =
                await dbContext.TenantDestroyReceipts
                    .Where(receipt =>
                        receipt.OperationId == request.IdempotencyKey ||
                        receipt.ScopeId == tenantId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
            if (receiptMatches.Length > 0)
            {
                GuestsTenantDestroyReceipt? exact = receiptMatches
                    .SingleOrDefault(receipt => receipt.ScopeId == tenantId);
                TenantTerminationContributionResult replay =
                    exact is not null &&
                    receiptMatches.Length == 1 &&
                    exact.Matches(request.IdempotencyKey, requestSha256)
                        ? Completed(exact)
                        : Failed(
                            "guests.termination.destroy-conflict",
                            clock.UtcNow);
                return await FinishAsync(
                    transaction,
                    replay,
                    cancellationToken).ConfigureAwait(false);
            }

            GuestsTenantDestroyOperation[] operationMatches =
                await dbContext.TenantDestroyOperations
                    .Where(operation =>
                        operation.OperationId == request.IdempotencyKey ||
                        operation.ScopeId == tenantId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
            GuestsTenantDestroyOperation? operation = operationMatches
                .SingleOrDefault(candidate => candidate.ScopeId == tenantId);
            if (operationMatches.Length > 0 &&
                (operation is null ||
                 operationMatches.Length != 1 ||
                 !operation.Matches(request.IdempotencyKey, requestSha256)))
            {
                return await FinishAsync(
                    transaction,
                    Failed(
                        "guests.termination.destroy-conflict",
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            GuestsTenantRevision? state = await dbContext.TenantRevisions
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (operation is null)
            {
                if (state is not null && !state.IsOpen)
                {
                    return await FinishAsync(
                        transaction,
                        Failed(
                            "guests.termination.destroy-conflict",
                            clock.UtcNow),
                        cancellationToken).ConfigureAwait(false);
                }

                long activeHoldCount = await dbContext.DataHolds.LongCountAsync(
                        hold => hold.State == GuestDataHoldState.Active,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (activeHoldCount > 0)
                {
                    return await FinishAsync(
                        transaction,
                        Blocked(
                            "guests.termination.destroy-active-hold",
                            activeHoldCount,
                            clock.UtcNow),
                        cancellationToken).ConfigureAwait(false);
                }

                long selectedRevision = state?.Revision ?? 0;
                DateTimeOffset operationStartedAtUtc = clock.UtcNow;
                operation = GuestsTenantDestroyOperation.TryCreate(
                    request.IdempotencyKey,
                    tenantId,
                    requestSha256,
                    selectedRevision,
                    DestroyBatchSize,
                    operationStartedAtUtc);
                if (state is null)
                {
                    state = GuestsTenantRevision.TryBeginClosing(
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
                            "guests.termination.destroy-state-invalid",
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
                state.LifecycleStatus != GuestsTenantLifecycleStatus.Closing ||
                !state.Matches(request.IdempotencyKey, requestSha256) ||
                state.Revision != operation.ResultingRevision)
            {
                return await FinishAsync(
                    transaction,
                    Failed(
                        "guests.termination.destroy-conflict",
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
                        "guests.termination.destroy-outbox-busy",
                        operation.RemovedRecordCount,
                        observedAtUtc),
                    cancellationToken).ConfigureAwait(false);
            }

            while (!operation.IsComplete)
            {
                bool removed = await this.RemoveCurrentStageAsync(
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
                        "guests.termination.destroy-in-progress",
                        operation.RemovedRecordCount,
                        clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            if (await this.HasRemainingOwnerRecordsAsync(
                    tenantId,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException(
                    "Guests tenant destruction completed with remaining owner records.");
            }

            DateTimeOffset completedAtUtc = clock.UtcNow;
            if (completedAtUtc > request.DeadlineUtc)
            {
                return await FinishAsync(
                    transaction,
                    RetryRequired(
                        "guests.termination.destroy-deadline-expired",
                        operation.RemovedRecordCount,
                        completedAtUtc),
                    cancellationToken).ConfigureAwait(false);
            }

            GuestsTenantDestroyReceipt? receipt =
                GuestsTenantDestroyReceipt.TryCreate(
                    operation,
                    completedAtUtc);
            if (receipt is null || !state.CompleteDestruction(completedAtUtc))
            {
                throw new InvalidDataException(
                    "Guests tenant destruction completion proof is invalid.");
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

        return GuestsTenantLifecycleHashes.Sha256(
            "bunkfy-guests-tenant-destroy-request/v1|" +
            $"{request.IdempotencyKey:N}|{Field(tenantId)}|" +
            $"{request.ProcessId:N}|{request.CaseId:N}|" +
            $"{request.ApprovalRevision.ToString(CultureInfo.InvariantCulture)}|" +
            $"{request.OperationRevision.ToString(CultureInfo.InvariantCulture)}|" +
            $"{request.TerminationEpoch:N}|{request.WorkItemId:N}|" +
            $"{request.PolicyEvidenceSha256}|" +
            $"{Field(request.ExecutingActorId)}|{DestroyBatchSize}");
    }

    private static TenantTerminationContributionResult Completed(
        GuestsTenantDestroyReceipt receipt) =>
        new(
            TenantTerminationContributionStatus.Completed,
            "guests.termination.destroyed",
            receipt.RemovedRecordCount,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 0,
            HoldReviewAtUtc: null,
            receipt.SelectedRevision,
            receipt.ResultingRevision,
            GuestsTenantTerminationMetadata.CatalogVersion,
            GuestsTenantTerminationMetadata.CatalogSha256,
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
            GuestsTenantTerminationMetadata.CatalogVersion,
            GuestsTenantTerminationMetadata.CatalogSha256,
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
            GuestsTenantTerminationMetadata.CatalogVersion,
            GuestsTenantTerminationMetadata.CatalogSha256,
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
}
