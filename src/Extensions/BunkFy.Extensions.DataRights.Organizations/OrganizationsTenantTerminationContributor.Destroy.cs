namespace BunkFy.Extensions.DataRights.Organizations;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.Organizations.Contracts;

internal sealed partial class OrganizationsTenantTerminationContributor
{
    private const int DestroyBatchSize =
        OrganizationScopeLifecycleLimits.MaximumDestroyBatchSize;

    public async Task<TenantTerminationContributionResult> ExecuteAsync(
        TenantTerminationContributionRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAtUtc = clock.UtcNow;
        if (request?.Phase == TenantTerminationContributionPhase.Export)
        {
            return Failed(
                "organizations.termination.export-requires-fragment-assembler",
                startedAtUtc);
        }

        if (request is null ||
            !IsValidDestroy(
                request,
                scopeContext,
                startedAtUtc,
                out Guid organizationId))
        {
            return Failed(
                "organizations.termination.destroy-request-invalid",
                startedAtUtc);
        }

        WorkspaceTerminationFenceSnapshot? fence =
            await this.ReadFenceAsync(cancellationToken)
                .ConfigureAwait(false);
        if (!Matches(request, fence))
        {
            return Retry(
                "organizations.termination.destroy-fence-unavailable",
                clock.UtcNow);
        }

        OrganizationScopeSnapshot selected = await lifecycle.GetSnapshotAsync(
                organizationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!TrySelectDestroyRevision(selected, out long expectedRevision))
        {
            return Failed(
                "organizations.termination.destroy-scope-invalid",
                clock.UtcNow);
        }

        OrganizationScopeDestroyResult result =
            await lifecycle.DestroyBatchAsync(
                    new OrganizationScopeDestroyRequest(
                        request.IdempotencyKey,
                        organizationId,
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
        OrganizationScopeDestroyResult? result,
        DateTimeOffset recordedAtUtc)
    {
        if (result is null)
        {
            return Failed(
                "organizations.termination.destroy-response-invalid",
                recordedAtUtc);
        }

        if (result.Status is OrganizationScopeDestroyStatus.Completed or
            OrganizationScopeDestroyStatus.Replayed)
        {
            return IsValid(
                result.Receipt,
                request,
                expectedRevision,
                recordedAtUtc)
                ? Completed(
                    "organizations.termination.destroyed",
                    result.Receipt!.RemovedRecordCount,
                    expectedRevision,
                    result.Receipt.ResultingRevision,
                    result.Receipt.CompletedAtUtc)
                : Failed(
                    "organizations.termination.destroy-response-invalid",
                    recordedAtUtc);
        }

        if (result.Status == OrganizationScopeDestroyStatus.InProgress)
        {
            return IsValid(
                result.Progress,
                request,
                expectedRevision,
                recordedAtUtc)
                ? Retry(
                    "organizations.termination.destroy-in-progress",
                    recordedAtUtc,
                    result.Progress!.RemovedRecordCount)
                : Failed(
                    "organizations.termination.destroy-response-invalid",
                    recordedAtUtc);
        }

        if (result.Status == OrganizationScopeDestroyStatus.Busy)
        {
            if (result.Progress is not null &&
                !IsValid(
                    result.Progress,
                    request,
                    expectedRevision,
                    recordedAtUtc))
            {
                return Failed(
                    "organizations.termination.destroy-response-invalid",
                    recordedAtUtc);
            }

            return Retry(
                "organizations.termination.destroy-scope-busy",
                recordedAtUtc,
                result.Progress?.RemovedRecordCount ?? 0);
        }

        return result.Status switch
        {
            OrganizationScopeDestroyStatus.Stale => Retry(
                "organizations.termination.destroy-revision-changed",
                recordedAtUtc),
            OrganizationScopeDestroyStatus.Conflict => Failed(
                "organizations.termination.destroy-conflict",
                recordedAtUtc),
            _ => Failed(
                "organizations.termination.destroy-response-invalid",
                recordedAtUtc)
        };
    }

    private static bool TrySelectDestroyRevision(
        OrganizationScopeSnapshot? snapshot,
        out long expectedRevision)
    {
        expectedRevision = 0;
        if (snapshot is null)
        {
            return false;
        }

        if (snapshot.Status == OrganizationScopeStatus.Missing &&
            snapshot.Revision == 0)
        {
            return true;
        }

        if (snapshot.Status == OrganizationScopeStatus.Open &&
            snapshot.Revision is >= 0 and < long.MaxValue)
        {
            expectedRevision = snapshot.Revision;
            return true;
        }

        if (snapshot.Status == OrganizationScopeStatus.Closed &&
            snapshot.Revision > 0)
        {
            expectedRevision = snapshot.Revision - 1;
            return true;
        }

        return false;
    }

    private static bool IsValid(
        OrganizationScopeDestroyProgress? progress,
        TenantTerminationContributionRequest request,
        long expectedRevision,
        DateTimeOffset recordedAtUtc) =>
        progress is not null &&
        progress.OperationId == request.IdempotencyKey &&
        progress.ResultingRevision == expectedRevision + 1 &&
        progress.BatchSize == DestroyBatchSize &&
        progress.Stage is > OrganizationScopeDestructionStage.Unknown and <
            OrganizationScopeDestructionStage.Completed &&
        progress.RemovedRecordCount >= 0 &&
        progress.CompletedBatchCount > 0 &&
        progress.RemovalProofVersion > 0 &&
        IsSha256(progress.RemovalProofSha256) &&
        progress.StartedAtUtc != default &&
        progress.UpdatedAtUtc >= progress.StartedAtUtc &&
        progress.UpdatedAtUtc <= recordedAtUtc;

    private static bool IsValid(
        OrganizationScopeDestroyReceipt? receipt,
        TenantTerminationContributionRequest request,
        long expectedRevision,
        DateTimeOffset recordedAtUtc) =>
        receipt is not null &&
        receipt.OperationId == request.IdempotencyKey &&
        receipt.ResultingRevision == expectedRevision + 1 &&
        receipt.BatchSize == DestroyBatchSize &&
        receipt.RemovedRecordCount >= 0 &&
        receipt.CompletedBatchCount >= 0 &&
        receipt.RemovalProofVersion > 0 &&
        IsSha256(receipt.RemovalProofSha256) &&
        receipt.StartedAtUtc != default &&
        receipt.CompletedAtUtc >= receipt.StartedAtUtc &&
        receipt.CompletedAtUtc <= recordedAtUtc;
}
