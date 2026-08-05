namespace BunkFy.Extensions.DataRights.AccessControl;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.AccessControl.Contracts;

internal sealed partial class AccessControlTenantTerminationContributor
{
    private const int DestroyBatchSize =
        AccessControlScopeLifecycleLimits.MaximumDestroyBatchSize;

    public async Task<TenantTerminationContributionResult> ExecuteAsync(
        TenantTerminationContributionRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAtUtc = clock.UtcNow;
        if (request?.Phase == TenantTerminationContributionPhase.Export)
        {
            return Failed(
                "access-control.termination.export-requires-fragment-assembler",
                startedAtUtc);
        }

        if (request is null ||
            !IsValidDestroy(
                request,
                scopeContext,
                startedAtUtc,
                out AccessControlScopeCoordinate coordinate))
        {
            return Failed(
                "access-control.termination.destroy-request-invalid",
                startedAtUtc);
        }

        WorkspaceTerminationFenceSnapshot? fence =
            await this.ReadFenceAsync(cancellationToken)
                .ConfigureAwait(false);
        if (!Matches(request, fence))
        {
            return Retry(
                "access-control.termination.destroy-fence-unavailable",
                clock.UtcNow);
        }

        AccessControlScopeSnapshot selected = await lifecycle.GetSnapshotAsync(
                coordinate,
                cancellationToken)
            .ConfigureAwait(false);
        if (!TrySelectDestroyRevision(selected, out long expectedRevision))
        {
            return Failed(
                "access-control.termination.destroy-scope-invalid",
                clock.UtcNow);
        }

        AccessControlScopeDestroyResult result =
            await lifecycle.DestroyBatchAsync(
                    new AccessControlScopeDestroyRequest(
                        request.IdempotencyKey,
                        coordinate,
                        expectedRevision,
                        DestroyBatchSize),
                    cancellationToken)
                .ConfigureAwait(false);
        return MapDestroyResult(
            request,
            expectedRevision,
            result,
            clock.UtcNow);
    }

    private static TenantTerminationContributionResult MapDestroyResult(
        TenantTerminationContributionRequest request,
        long expectedRevision,
        AccessControlScopeDestroyResult? result,
        DateTimeOffset recordedAtUtc)
    {
        if (result is null)
        {
            return Failed(
                "access-control.termination.destroy-response-invalid",
                recordedAtUtc);
        }

        if (result.Status is AccessControlScopeDestroyStatus.Completed or
            AccessControlScopeDestroyStatus.Replayed)
        {
            return IsValid(
                result.Receipt,
                request,
                expectedRevision,
                recordedAtUtc)
                ? Completed(
                    "access-control.termination.destroyed",
                    result.Receipt!.RemovedRecordCount,
                    expectedRevision,
                    result.Receipt.ResultingRevision,
                    result.Receipt.CompletedAtUtc)
                : Failed(
                    "access-control.termination.destroy-response-invalid",
                    recordedAtUtc);
        }

        if (result.Status == AccessControlScopeDestroyStatus.InProgress)
        {
            return IsValid(
                result.Progress,
                request,
                expectedRevision,
                recordedAtUtc)
                ? Retry(
                    "access-control.termination.destroy-in-progress",
                    recordedAtUtc,
                    result.Progress!.RemovedRecordCount)
                : Failed(
                    "access-control.termination.destroy-response-invalid",
                    recordedAtUtc);
        }

        if (result.Status == AccessControlScopeDestroyStatus.Busy)
        {
            if (result.Progress is not null &&
                !IsValid(
                    result.Progress,
                    request,
                    expectedRevision,
                    recordedAtUtc))
            {
                return Failed(
                    "access-control.termination.destroy-response-invalid",
                    recordedAtUtc);
            }

            return Retry(
                "access-control.termination.destroy-scope-busy",
                recordedAtUtc,
                result.Progress?.RemovedRecordCount ?? 0);
        }

        return result.Status switch
        {
            AccessControlScopeDestroyStatus.Stale => Retry(
                "access-control.termination.destroy-revision-changed",
                recordedAtUtc),
            AccessControlScopeDestroyStatus.Conflict => Failed(
                "access-control.termination.destroy-conflict",
                recordedAtUtc),
            _ => Failed(
                "access-control.termination.destroy-response-invalid",
                recordedAtUtc)
        };
    }

    private static bool TrySelectDestroyRevision(
        AccessControlScopeSnapshot? snapshot,
        out long expectedRevision)
    {
        expectedRevision = 0;
        if (snapshot is null)
        {
            return false;
        }

        if (snapshot.Status == AccessControlScopeStatus.Missing &&
            snapshot.Revision == 0 &&
            !snapshot.SelectedRevision.HasValue)
        {
            return true;
        }

        if (snapshot.Status == AccessControlScopeStatus.Open &&
            snapshot.Revision is >= 0 and < long.MaxValue &&
            !snapshot.SelectedRevision.HasValue)
        {
            expectedRevision = snapshot.Revision;
            return true;
        }

        if (snapshot.Status == AccessControlScopeStatus.Closed &&
            snapshot.Revision > 0 &&
            snapshot.SelectedRevision is >= 0 and < long.MaxValue &&
            snapshot.SelectedRevision.Value < snapshot.Revision)
        {
            expectedRevision = snapshot.SelectedRevision.Value;
            return true;
        }

        return false;
    }

    private static bool IsValid(
        AccessControlScopeDestroyProgress? progress,
        TenantTerminationContributionRequest request,
        long expectedRevision,
        DateTimeOffset recordedAtUtc) =>
        progress is not null &&
        progress.OperationId == request.IdempotencyKey &&
        IsResultingRevision(expectedRevision, progress.ResultingRevision) &&
        progress.BatchSize == DestroyBatchSize &&
        progress.Stage is > AccessControlScopeDestructionStage.Unknown and <
            AccessControlScopeDestructionStage.Completed &&
        progress.RemovedRecordCount > 0 &&
        progress.CompletedBatchCount > 0 &&
        progress.RemovalProofVersion > 0 &&
        IsSha256(progress.RemovalProofSha256) &&
        progress.StartedAtUtc != default &&
        progress.UpdatedAtUtc >= progress.StartedAtUtc &&
        progress.UpdatedAtUtc <= recordedAtUtc;

    private static bool IsValid(
        AccessControlScopeDestroyReceipt? receipt,
        TenantTerminationContributionRequest request,
        long expectedRevision,
        DateTimeOffset recordedAtUtc) =>
        receipt is not null &&
        receipt.OperationId == request.IdempotencyKey &&
        IsResultingRevision(expectedRevision, receipt.ResultingRevision) &&
        receipt.BatchSize == DestroyBatchSize &&
        receipt.RemovedRecordCount >= 0 &&
        ((receipt.RemovedRecordCount == 0 &&
          receipt.CompletedBatchCount == 0) ||
         (receipt.RemovedRecordCount > 0 &&
          receipt.CompletedBatchCount > 0)) &&
        receipt.RemovalProofVersion > 0 &&
        IsSha256(receipt.RemovalProofSha256) &&
        receipt.StartedAtUtc != default &&
        receipt.CompletedAtUtc >= receipt.StartedAtUtc &&
        receipt.CompletedAtUtc <= recordedAtUtc;

    private static bool IsResultingRevision(
        long expectedRevision,
        long resultingRevision) =>
        expectedRevision == 0
            ? resultingRevision > 0
            : resultingRevision == expectedRevision + 1;
}
