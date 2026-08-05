namespace BunkFy.Extensions.DataRights.AccessControl;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Naming;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;

internal sealed partial class AccessControlTenantTerminationContributor(
    IAccessControlScopeLifecycle lifecycle,
    IScopeContext scopeContext,
    ISystemClock clock,
    IWorkspaceTerminationFenceReader? terminationFences = null)
    : ITenantTerminationContributor,
      ITenantTerminationExportContributor
{
    public TenantTerminationContributorDescriptor Descriptor { get; } = new(
        AccessControlTenantTerminationMetadata.OwnerKey,
        TenantTerminationContract.CurrentVersion,
        [
            new(
                TenantTerminationContributionPhase.Export,
                AccessControlTenantTerminationMetadata.DependencyOwnerKeys),
            new(
                TenantTerminationContributionPhase.Destroy,
                AccessControlTenantTerminationMetadata.DependencyOwnerKeys)
        ],
        MandatoryForProduction: true,
        AccessControlTenantTerminationMetadata.CatalogVersion,
        AccessControlTenantTerminationMetadata.CatalogSha256);

    public DataRightsExportDescriptor ExportDescriptor =>
        AccessControlTenantTerminationExportSchema.Descriptor;

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
        TenantTerminationExportRequest request,
        WorkspaceTerminationFenceSnapshot? fence) =>
        fence is not null &&
        fence.ProcessId == request.Contribution.ProcessId &&
        fence.TerminationEpoch == request.Contribution.TerminationEpoch &&
        fence.State == WorkspaceTerminationFenceState.Frozen &&
        fence.Version == request.WorkspaceFenceRevision;

    private static bool Matches(
        TenantTerminationContributionRequest request,
        WorkspaceTerminationFenceSnapshot? fence) =>
        fence is not null &&
        fence.ProcessId == request.ProcessId &&
        fence.TerminationEpoch == request.TerminationEpoch &&
        fence.State == WorkspaceTerminationFenceState.Frozen;

    private static bool IsValid(
        TenantTerminationExportRequest request,
        IScopeContext scope,
        DateTimeOffset nowUtc,
        out AccessControlScopeCoordinate coordinate)
    {
        coordinate = null!;
        TenantTerminationContributionRequest contribution =
            request.Contribution;
        return contribution.ContractVersion ==
                TenantTerminationContract.CurrentVersion &&
            TryCoordinate(contribution.TenantId, out coordinate) &&
            scope.IsEnabled &&
            string.Equals(
                scope.ScopeId,
                contribution.TenantId,
                StringComparison.Ordinal) &&
            contribution.ProcessId != Guid.Empty &&
            contribution.CaseId != Guid.Empty &&
            contribution.ApprovalRevision > 0 &&
            request.FreezeOperationRevision > 0 &&
            contribution.OperationRevision >
                request.FreezeOperationRevision &&
            contribution.TerminationEpoch != Guid.Empty &&
            contribution.Phase ==
                TenantTerminationContributionPhase.Export &&
            contribution.WorkItemId != Guid.Empty &&
            contribution.IdempotencyKey != Guid.Empty &&
            IsSha256(contribution.PolicyEvidenceSha256) &&
            IsSha256(request.FrozenRevisionSha256) &&
            request.WorkspaceFenceRevision > 0 &&
            request.FrozenAtUtc != default &&
            request.FrozenAtUtc <= nowUtc &&
            contribution.DeadlineUtc > nowUtc &&
            IsActor(contribution.ExecutingActorId);
    }

    private static bool IsValidDestroy(
        TenantTerminationContributionRequest request,
        IScopeContext scope,
        DateTimeOffset nowUtc,
        out AccessControlScopeCoordinate coordinate)
    {
        coordinate = null!;
        return request.ContractVersion ==
                TenantTerminationContract.CurrentVersion &&
            TryCoordinate(request.TenantId, out coordinate) &&
            scope.IsEnabled &&
            string.Equals(
                scope.ScopeId,
                request.TenantId,
                StringComparison.Ordinal) &&
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

    private static bool TryCoordinate(
        string? tenantId,
        out AccessControlScopeCoordinate coordinate)
    {
        coordinate = null!;
        if (!TenantIds.TryNormalize(tenantId, out string? normalized) ||
            !string.Equals(tenantId, normalized, StringComparison.Ordinal) ||
            !Guid.TryParseExact(normalized, "D", out Guid workspaceId) ||
            workspaceId == Guid.Empty ||
            !string.Equals(
                workspaceId.ToString("D"),
                normalized,
                StringComparison.Ordinal))
        {
            return false;
        }

        coordinate = new AccessControlScopeCoordinate(
            WorkspaceAccessScopes.Create(normalized),
            normalized);
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
        string code,
        long affectedCount,
        long selectedRevision,
        long resultingRevision,
        DateTimeOffset recordedAtUtc) =>
        new(
            TenantTerminationContributionStatus.Completed,
            code,
            affectedCount,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 0,
            HoldReviewAtUtc: null,
            selectedRevision,
            resultingRevision,
            AccessControlTenantTerminationMetadata.CatalogVersion,
            AccessControlTenantTerminationMetadata.CatalogSha256,
            recordedAtUtc);

    private static TenantTerminationContributionResult Retry(
        string code,
        DateTimeOffset recordedAtUtc,
        long affectedCount = 0) =>
        new(
            TenantTerminationContributionStatus.RetryRequired,
            code,
            affectedCount,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 1,
            HoldReviewAtUtc: null,
            SelectedProofRevision: null,
            ResultingProofRevision: null,
            AccessControlTenantTerminationMetadata.CatalogVersion,
            AccessControlTenantTerminationMetadata.CatalogSha256,
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
            AccessControlTenantTerminationMetadata.CatalogVersion,
            AccessControlTenantTerminationMetadata.CatalogSha256,
            recordedAtUtc);
}
