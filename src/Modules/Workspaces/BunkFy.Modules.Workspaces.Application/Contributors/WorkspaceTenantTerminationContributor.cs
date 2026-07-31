namespace BunkFy.Modules.Workspaces.Application.Contributors;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.Termination;
using Gma.Framework.Cqrs;
using Gma.Framework.Naming;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class WorkspaceTenantTerminationContributor(
    IRequestDispatcher dispatcher,
    IWorkspaceTerminationFenceRepository fences,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ITenantTerminationContributor
{
    private const int CatalogVersion = 1;
    private static readonly string CatalogSha256 = Convert.ToHexStringLower(
        SHA256.HashData(
            Encoding.UTF8.GetBytes(
                "workspaces|contract=1|phases=freeze,restore|" +
                "dependencies=|mandatory=true|catalog=1")));

    public TenantTerminationContributorDescriptor Descriptor { get; } = new(
        WorkspacesDataRightsCoordinates.Owner,
        TenantTerminationContract.CurrentVersion,
        [
            TenantTerminationContributionPhase.Freeze,
            TenantTerminationContributionPhase.Restore
        ],
        [],
        MandatoryForProduction: true,
        CatalogVersion,
        CatalogSha256);

    public async Task<TenantTerminationContributionResult> ExecuteAsync(
        TenantTerminationContributionRequest request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset nowUtc = clock.UtcNow;
        if (!IsValid(request, scopeContext, nowUtc))
        {
            return Failed("workspace.termination.request-invalid", nowUtc);
        }

        WorkspaceTerminationFenceReceipt? replay =
            await fences.FindReceiptAsync(
                request.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return this.ToResult(request, replay.ToDto());
        }

        Result<WorkspaceTerminationFenceReceiptDto> executed =
            request.Phase switch
            {
                TenantTerminationContributionPhase.Freeze =>
                    await dispatcher.SendAsync(
                        new ApplyWorkspaceTerminationFenceCommand(
                            request.IdempotencyKey,
                            request.ProcessId,
                            request.CaseId,
                            request.ApprovalRevision,
                            request.OperationRevision,
                            request.WorkItemId,
                            request.TerminationEpoch,
                            request.PolicyEvidenceSha256,
                            request.ExecutingActorId),
                        cancellationToken).ConfigureAwait(false),
                TenantTerminationContributionPhase.Restore =>
                    await this.ReleaseAsync(
                        request,
                        cancellationToken).ConfigureAwait(false),
                _ => Result.Failure<WorkspaceTerminationFenceReceiptDto>(
                    WorkspaceTerminationApplicationErrors.RequestInvalid)
            };

        if (executed.IsFailure)
        {
            return IsBlocked(executed.Error.Code)
                ? Blocked(ToStableResultCode(executed.Error.Code), nowUtc)
                : Failed(ToStableResultCode(executed.Error.Code), nowUtc);
        }

        return this.ToResult(request, executed.Value);
    }

    private async Task<Result<WorkspaceTerminationFenceReceiptDto>>
        ReleaseAsync(
            TenantTerminationContributionRequest request,
            CancellationToken cancellationToken)
    {
        WorkspaceTerminationFence? fence = await fences.GetByProcessAsync(
            request.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (fence is null)
        {
            return Result.Failure<WorkspaceTerminationFenceReceiptDto>(
                WorkspaceTerminationApplicationErrors.FenceNotFound);
        }

        return await dispatcher.SendAsync(
            new ReleaseWorkspaceTerminationFenceCommand(
                request.IdempotencyKey,
                request.ProcessId,
                request.CaseId,
                request.ApprovalRevision,
                request.OperationRevision,
                request.WorkItemId,
                request.TerminationEpoch,
                fence.Version,
                request.PolicyEvidenceSha256,
                request.ExecutingActorId),
            cancellationToken).ConfigureAwait(false);
    }

    private TenantTerminationContributionResult ToResult(
        TenantTerminationContributionRequest request,
        WorkspaceTerminationFenceReceiptDto receipt)
    {
        WorkspaceTerminationFenceActionDto expectedAction =
            request.Phase == TenantTerminationContributionPhase.Freeze
                ? WorkspaceTerminationFenceActionDto.Freeze
                : WorkspaceTerminationFenceActionDto.Release;
        if (receipt.Action != expectedAction ||
            receipt.ProcessId != request.ProcessId ||
            receipt.CaseId != request.CaseId ||
            receipt.ApprovalRevision != request.ApprovalRevision ||
            receipt.OperationRevision != request.OperationRevision ||
            receipt.WorkItemId != request.WorkItemId ||
            receipt.IdempotencyKey != request.IdempotencyKey ||
            receipt.TerminationEpoch != request.TerminationEpoch ||
            !string.Equals(
                receipt.PolicyEvidenceSha256,
                request.PolicyEvidenceSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                receipt.ActorId,
                request.ExecutingActorId.Trim(),
                StringComparison.Ordinal) ||
            receipt.CompletedAtUtc == default ||
            receipt.CompletedAtUtc > request.DeadlineUtc)
        {
            return Failed(
                "workspace.termination.owner-proof-invalid",
                clock.UtcNow);
        }

        return new TenantTerminationContributionResult(
            TenantTerminationContributionStatus.Completed,
            expectedAction == WorkspaceTerminationFenceActionDto.Freeze
                ? "workspace.termination.frozen"
                : "workspace.termination.released",
            AffectedCount: 1,
            RetainedMinimumCount: 0,
            RemainingActiveCount: 0,
            HoldReviewAtUtc: null,
            receipt.SelectedFenceVersion,
            receipt.ResultingFenceVersion,
            CatalogVersion,
            CatalogSha256,
            receipt.CompletedAtUtc);
    }

    private static bool IsValid(
        TenantTerminationContributionRequest? request,
        IScopeContext scopeContext,
        DateTimeOffset nowUtc)
    {
        if (request is null ||
            request.ContractVersion !=
                TenantTerminationContract.CurrentVersion ||
            request.ProcessId == Guid.Empty ||
            request.CaseId == Guid.Empty ||
            request.ApprovalRevision < 1 ||
            request.OperationRevision < 1 ||
            request.TerminationEpoch == Guid.Empty ||
            request.WorkItemId == Guid.Empty ||
            request.IdempotencyKey == Guid.Empty ||
            request.Phase is not (
                TenantTerminationContributionPhase.Freeze or
                TenantTerminationContributionPhase.Restore) ||
            request.DeadlineUtc <= nowUtc ||
            !IsSha256(request.PolicyEvidenceSha256) ||
            !IsActor(request.ExecutingActorId) ||
            !scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId) ||
            !TenantIds.TryNormalize(request.TenantId, out string? tenantId))
        {
            return false;
        }

        return string.Equals(
            scopeContext.ScopeId,
            tenantId,
            StringComparison.Ordinal);
    }

    private static bool IsBlocked(string errorCode) =>
        errorCode is
            "Workspaces.TerminationFenceActiveConflict"
            or "Workspaces.TerminationFenceNotFound"
            or "Workspaces.TerminationFenceCoordinatesConflict"
            or "Workspaces.TerminationFenceVersionConflict"
            or "Workspaces.TerminationFenceTransitionInvalid";

    private static string ToStableResultCode(string errorCode)
    {
        string suffix = errorCode.StartsWith(
            "Workspaces.",
            StringComparison.Ordinal)
            ? errorCode["Workspaces.".Length..]
            : "failed";
        return "workspace." + string.Concat(
            suffix.Select(character =>
                char.IsUpper(character)
                    ? $"-{char.ToLowerInvariant(character)}"
                    : character.ToString())).TrimStart('-');
    }

    private static TenantTerminationContributionResult Blocked(
        string code,
        DateTimeOffset nowUtc) =>
        new(
            TenantTerminationContributionStatus.Blocked,
            code,
            0,
            0,
            1,
            null,
            null,
            null,
            CatalogVersion,
            CatalogSha256,
            nowUtc);

    private static TenantTerminationContributionResult Failed(
        string code,
        DateTimeOffset nowUtc) =>
        new(
            TenantTerminationContributionStatus.Failed,
            code,
            0,
            0,
            1,
            null,
            null,
            null,
            CatalogVersion,
            CatalogSha256,
            nowUtc);

    private static bool IsActor(string? actorId)
    {
        string normalized = actorId?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <=
            TenantTerminationContract.ActorIdMaxLength;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: TenantTerminationContract.Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
