namespace BunkFy.Extensions.DataRights.TaskRuntime;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.TaskRuntime.Contracts;

internal sealed partial class TaskRuntimeTenantTerminationContributor
{
    private const int DestroyBatchSize =
        TaskRuntimeScopeLifecycleLimits.MaximumDestroyBatchSize;

    public async Task<TenantTerminationContributionResult> ExecuteAsync(
        TenantTerminationContributionRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAtUtc = clock.UtcNow;
        if (request is null ||
            !IsValidDestroy(
                request,
                scopeContext,
                startedAtUtc,
                out string tenantId))
        {
            return Failed(
                request?.Phase == TenantTerminationContributionPhase.Export
                    ? "task-runtime.termination.export-not-supported"
                    : "task-runtime.termination.destroy-request-invalid",
                startedAtUtc);
        }

        WorkspaceTerminationFenceSnapshot? fence =
            await this.ReadFenceAsync(cancellationToken).ConfigureAwait(false);
        if (!Matches(request, fence))
        {
            return Retry(
                "task-runtime.termination.destroy-fence-unavailable",
                clock.UtcNow);
        }

        TaskRuntimeScopeSnapshot selected = await lifecycle.GetSnapshotAsync(
                tenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!TrySelectDestroyRevision(selected, out long expectedRevision))
        {
            return Failed(
                "task-runtime.termination.destroy-scope-invalid",
                clock.UtcNow);
        }

        TaskRuntimeScopeDestroyResult result = await lifecycle.DestroyBatchAsync(
                new TaskRuntimeScopeDestroyRequest(
                    request.IdempotencyKey,
                    tenantId,
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
        TaskRuntimeScopeDestroyResult? result,
        DateTimeOffset recordedAtUtc)
    {
        if (result is null)
        {
            return Failed(
                "task-runtime.termination.destroy-response-invalid",
                recordedAtUtc);
        }

        if (result.Status is TaskRuntimeScopeDestroyStatus.Completed or
            TaskRuntimeScopeDestroyStatus.Replayed)
        {
            return IsValid(
                result.Receipt,
                request,
                expectedRevision,
                recordedAtUtc)
                ? Completed(
                    result.Receipt!.RemovedRecordCount,
                    expectedRevision,
                    result.Receipt.ResultingRevision,
                    result.Receipt.CompletedAtUtc)
                : Failed(
                    "task-runtime.termination.destroy-response-invalid",
                    recordedAtUtc);
        }

        if (result.Status == TaskRuntimeScopeDestroyStatus.InProgress)
        {
            return IsValid(
                result.Progress,
                request,
                expectedRevision,
                recordedAtUtc)
                ? Retry(
                    "task-runtime.termination.destroy-in-progress",
                    recordedAtUtc,
                    result.Progress!.RemovedRecordCount,
                    result.Progress.RemainingActiveRunCount)
                : Failed(
                    "task-runtime.termination.destroy-response-invalid",
                    recordedAtUtc);
        }

        if (result.Status == TaskRuntimeScopeDestroyStatus.Busy)
        {
            return IsValidBusy(
                result.Progress,
                request,
                expectedRevision,
                recordedAtUtc)
                ? Retry(
                    "task-runtime.termination.destroy-scope-busy",
                    recordedAtUtc,
                    result.Progress!.RemovedRecordCount,
                    result.Progress.RemainingActiveRunCount)
                : Failed(
                    "task-runtime.termination.destroy-response-invalid",
                    recordedAtUtc);
        }

        return result.Status switch
        {
            TaskRuntimeScopeDestroyStatus.Stale => Retry(
                "task-runtime.termination.destroy-revision-changed",
                recordedAtUtc),
            TaskRuntimeScopeDestroyStatus.Conflict => Failed(
                "task-runtime.termination.destroy-conflict",
                recordedAtUtc),
            _ => Failed(
                "task-runtime.termination.destroy-response-invalid",
                recordedAtUtc)
        };
    }

    private static bool TrySelectDestroyRevision(
        TaskRuntimeScopeSnapshot? snapshot,
        out long expectedRevision)
    {
        expectedRevision = 0;
        if (snapshot is null ||
            snapshot.TotalRunCount < 0 ||
            snapshot.ActiveRunCount < 0 ||
            snapshot.ActiveRunCount > snapshot.TotalRunCount ||
            snapshot.ControlMessageCount < 0)
        {
            return false;
        }

        if (snapshot.Status == TaskRuntimeScopeStatus.Missing)
        {
            return snapshot.Revision == 0 &&
                !snapshot.SelectedRevision.HasValue &&
                snapshot.TotalRunCount == 0 &&
                snapshot.ActiveRunCount == 0 &&
                snapshot.ControlMessageCount == 0;
        }

        if (snapshot.Status == TaskRuntimeScopeStatus.Open)
        {
            return snapshot.Revision == 0 &&
                !snapshot.SelectedRevision.HasValue &&
                (snapshot.TotalRunCount > 0 ||
                 snapshot.ControlMessageCount > 0);
        }

        if ((snapshot.Status is TaskRuntimeScopeStatus.Closing or
             TaskRuntimeScopeStatus.Closed) &&
            snapshot.SelectedRevision is >= 0 and < long.MaxValue &&
            snapshot.Revision == snapshot.SelectedRevision.Value + 1)
        {
            if (snapshot.Status == TaskRuntimeScopeStatus.Closed &&
                (snapshot.TotalRunCount != 0 ||
                 snapshot.ActiveRunCount != 0 ||
                 snapshot.ControlMessageCount != 0))
            {
                return false;
            }

            expectedRevision = snapshot.SelectedRevision.Value;
            return true;
        }

        return false;
    }

    private static bool IsValid(
        TaskRuntimeScopeDestroyProgress? progress,
        TenantTerminationContributionRequest request,
        long expectedRevision,
        DateTimeOffset recordedAtUtc) =>
        IsValidProgressShape(
            progress,
            request,
            expectedRevision,
            recordedAtUtc) &&
        progress!.Stage is > TaskRuntimeScopeDestructionStage.Unknown and <
            TaskRuntimeScopeDestructionStage.Completed;

    private static bool IsValidBusy(
        TaskRuntimeScopeDestroyProgress? progress,
        TenantTerminationContributionRequest request,
        long expectedRevision,
        DateTimeOffset recordedAtUtc) =>
        IsValidProgressShape(
            progress,
            request,
            expectedRevision,
            recordedAtUtc) &&
        progress!.Stage == TaskRuntimeScopeDestructionStage.QuiesceRuns &&
        progress.RemainingActiveRunCount > 0;

    private static bool IsValidProgressShape(
        TaskRuntimeScopeDestroyProgress? progress,
        TenantTerminationContributionRequest request,
        long expectedRevision,
        DateTimeOffset recordedAtUtc) =>
        progress is not null &&
        progress.OperationId == request.IdempotencyKey &&
        progress.SelectedRevision == expectedRevision &&
        IsResultingRevision(expectedRevision, progress.ResultingRevision) &&
        progress.BatchSize == DestroyBatchSize &&
        progress.RemovedRecordCount >= 0 &&
        ((progress.RemovedRecordCount == 0 &&
          progress.CompletedBatchCount == 0) ||
         (progress.RemovedRecordCount > 0 &&
          progress.CompletedBatchCount > 0)) &&
        progress.RemainingActiveRunCount >= 0 &&
        progress.RemovalProofVersion > 0 &&
        IsSha256(progress.RemovalProofSha256) &&
        progress.StartedAtUtc != default &&
        progress.UpdatedAtUtc >= progress.StartedAtUtc &&
        progress.UpdatedAtUtc <= recordedAtUtc;

    private static bool IsValid(
        TaskRuntimeScopeDestroyReceipt? receipt,
        TenantTerminationContributionRequest request,
        long expectedRevision,
        DateTimeOffset recordedAtUtc) =>
        receipt is not null &&
        receipt.OperationId == request.IdempotencyKey &&
        receipt.SelectedRevision == expectedRevision &&
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
        expectedRevision is >= 0 and < long.MaxValue &&
        resultingRevision == expectedRevision + 1;
}
