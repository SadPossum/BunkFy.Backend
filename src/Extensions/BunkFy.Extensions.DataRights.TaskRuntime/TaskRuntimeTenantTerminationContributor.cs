namespace BunkFy.Extensions.DataRights.TaskRuntime;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.TaskRuntime.Contracts;

internal sealed partial class TaskRuntimeTenantTerminationContributor(
    ITaskRuntimeScopeLifecycle lifecycle,
    IScopeContext scopeContext,
    ISystemClock clock,
    IWorkspaceTerminationFenceReader? terminationFences = null)
    : ITenantTerminationContributor
{
    public TenantTerminationContributorDescriptor Descriptor { get; } = new(
        TaskRuntimeTenantTerminationMetadata.OwnerKey,
        TenantTerminationContract.CurrentVersion,
        [
            new(
                TenantTerminationContributionPhase.Destroy,
                TaskRuntimeTenantTerminationMetadata.DependencyOwnerKeys,
                TenantTerminationExecutionBoundary.GlobalControlTask)
        ],
        MandatoryForProduction: true,
        TaskRuntimeTenantTerminationMetadata.CatalogVersion,
        TaskRuntimeTenantTerminationMetadata.CatalogSha256);

    private async Task<WorkspaceTerminationFenceSnapshot?> ReadFenceAsync(
        CancellationToken cancellationToken)
    {
        if (terminationFences is null)
        {
            return null;
        }

        try
        {
            return await terminationFences.GetCurrentAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private static bool Matches(
        TenantTerminationContributionRequest request,
        WorkspaceTerminationFenceSnapshot? fence) =>
        fence is not null &&
        fence.ProcessId == request.ProcessId &&
        fence.TerminationEpoch == request.TerminationEpoch &&
        fence.State == WorkspaceTerminationFenceState.Frozen;

    private static bool IsValidDestroy(
        TenantTerminationContributionRequest request,
        IScopeContext scope,
        DateTimeOffset nowUtc,
        out string tenantId)
    {
        tenantId = string.Empty;
        return request.ContractVersion ==
                TenantTerminationContract.CurrentVersion &&
            TryTenantId(request.TenantId, out tenantId) &&
            scope.IsEnabled &&
            string.Equals(scope.ScopeId, tenantId, StringComparison.Ordinal) &&
            request.ProcessId != Guid.Empty &&
            request.CaseId != Guid.Empty &&
            request.ApprovalRevision > 0 &&
            request.OperationRevision > 0 &&
            request.TerminationEpoch != Guid.Empty &&
            request.Phase == TenantTerminationContributionPhase.Destroy &&
            request.WorkItemId != Guid.Empty &&
            request.IdempotencyKey != Guid.Empty &&
            IsSha256(request.PolicyEvidenceSha256) &&
            request.DeadlineUtc > nowUtc &&
            IsActor(request.ExecutingActorId);
    }

    private static bool TryTenantId(string? value, out string tenantId)
    {
        tenantId = string.Empty;
        if (!TenantIds.TryNormalize(value, out string? normalized) ||
            !string.Equals(value, normalized, StringComparison.Ordinal) ||
            !Guid.TryParseExact(normalized, "D", out Guid workspaceId) ||
            workspaceId == Guid.Empty ||
            !string.Equals(
                workspaceId.ToString("D"),
                normalized,
                StringComparison.Ordinal))
        {
            return false;
        }

        tenantId = normalized;
        return true;
    }

    private static bool IsActor(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <=
                TenantTerminationContract.ActorIdMaxLength &&
            string.Equals(value, normalized, StringComparison.Ordinal) &&
            !normalized.Any(char.IsControl);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: TenantTerminationContract.Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static TenantTerminationContributionResult Completed(
        long affectedCount,
        long selectedRevision,
        long resultingRevision,
        DateTimeOffset recordedAtUtc) =>
        new(
            TenantTerminationContributionStatus.Completed,
            "task-runtime.termination.destroyed",
            affectedCount,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 0,
            HoldReviewAtUtc: null,
            selectedRevision,
            resultingRevision,
            TaskRuntimeTenantTerminationMetadata.CatalogVersion,
            TaskRuntimeTenantTerminationMetadata.CatalogSha256,
            recordedAtUtc);

    private static TenantTerminationContributionResult Retry(
        string code,
        DateTimeOffset recordedAtUtc,
        long affectedCount = 0,
        long remainingActiveCount = 1) =>
        new(
            TenantTerminationContributionStatus.RetryRequired,
            code,
            affectedCount,
            RetainedMinimumCount: 0,
            RemainingActiveCount: Math.Max(1, remainingActiveCount),
            HoldReviewAtUtc: null,
            SelectedProofRevision: null,
            ResultingProofRevision: null,
            TaskRuntimeTenantTerminationMetadata.CatalogVersion,
            TaskRuntimeTenantTerminationMetadata.CatalogSha256,
            recordedAtUtc);

    private static TenantTerminationContributionResult Failed(
        string code,
        DateTimeOffset recordedAtUtc) =>
        new(
            TenantTerminationContributionStatus.Failed,
            code,
            AffectedCount: 0,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 1,
            HoldReviewAtUtc: null,
            SelectedProofRevision: null,
            ResultingProofRevision: null,
            TaskRuntimeTenantTerminationMetadata.CatalogVersion,
            TaskRuntimeTenantTerminationMetadata.CatalogSha256,
            recordedAtUtc);
}
