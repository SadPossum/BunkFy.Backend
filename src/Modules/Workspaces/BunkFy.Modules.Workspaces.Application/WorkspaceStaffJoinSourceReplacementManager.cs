namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Modules.Organizations.Contracts;

public interface IWorkspaceStaffJoinSourceReplacementManager
{
    Task<Result<WorkspaceStaffJoinSourceReplacementDto>> ReplaceInvitationAsync(
        Guid sourceId,
        Guid replacementSourceId,
        long expectedVersion,
        int lifetimeHours,
        string actorSubjectId,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceStaffJoinSourceReplacementDto>> ReplaceEnrollmentLinkAsync(
        Guid sourceId,
        Guid replacementSourceId,
        long expectedVersion,
        int lifetimeHours,
        string actorSubjectId,
        CancellationToken cancellationToken = default);
}

internal sealed class WorkspaceStaffJoinSourceReplacementManager(
    IOrganizationJoinSourceManager organizations,
    IWorkspaceStaffAccessPlanRepository plans,
    IWorkspaceStaffJoinSourceIssuer issuer,
    IScopeContext scopeContext) : IWorkspaceStaffJoinSourceReplacementManager
{
    public async Task<Result<WorkspaceStaffJoinSourceReplacementDto>> ReplaceInvitationAsync(
        Guid sourceId,
        Guid replacementSourceId,
        long expectedVersion,
        int lifetimeHours,
        string actorSubjectId,
        CancellationToken cancellationToken = default)
    {
        Result<ReplacementContext> context = this.Validate(
            sourceId, replacementSourceId, expectedVersion, lifetimeHours, actorSubjectId);
        if (context.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceReplacementDto>(context.Error);
        }

        Result<WorkspaceStaffAccessPlan> plan = await this.GetPlanAsync(
            sourceId, WorkspaceStaffOnboardingSource.Invitation, cancellationToken)
            .ConfigureAwait(false);
        if (plan.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceReplacementDto>(plan.Error);
        }

        OrganizationJoinSourceOperation<OrganizationInvitationDto> selected =
            await organizations.GetInvitationAsync(
                new OrganizationJoinSourceLookupRequest(
                    context.Value.OrganizationId, sourceId, context.Value.ActorId),
                cancellationToken).ConfigureAwait(false);
        if (!selected.IsSuccess)
        {
            return ManagementFailure();
        }

        Result<OrganizationInvitationDto> denied = await this.DenyInvitationAsync(
            selected.Value!, expectedVersion, context.Value, cancellationToken).ConfigureAwait(false);
        if (denied.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceReplacementDto>(denied.Error);
        }

        Result<WorkspaceStaffJoinSourceIssuanceDto> replacement = await issuer.IssueInvitationAsync(
            new WorkspaceInvitationIssuanceRequest(
                replacementSourceId,
                selected.Value!.RecipientEmail,
                lifetimeHours,
                plan.Value.ProfileKey,
                plan.Value.Properties.Select(item => item.PropertyId).ToArray(),
                context.Value.ActorId),
            cancellationToken).ConfigureAwait(false);
        return replacement.IsFailure
            ? Result.Failure<WorkspaceStaffJoinSourceReplacementDto>(replacement.Error)
            : Result.Success(new WorkspaceStaffJoinSourceReplacementDto(
                sourceId, Map(denied.Value.Status), denied.Value.Version, replacement.Value));
    }

    public async Task<Result<WorkspaceStaffJoinSourceReplacementDto>> ReplaceEnrollmentLinkAsync(
        Guid sourceId,
        Guid replacementSourceId,
        long expectedVersion,
        int lifetimeHours,
        string actorSubjectId,
        CancellationToken cancellationToken = default)
    {
        Result<ReplacementContext> context = this.Validate(
            sourceId, replacementSourceId, expectedVersion, lifetimeHours, actorSubjectId);
        if (context.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceReplacementDto>(context.Error);
        }

        Result<WorkspaceStaffAccessPlan> plan = await this.GetPlanAsync(
            sourceId, WorkspaceStaffOnboardingSource.EnrollmentLink, cancellationToken)
            .ConfigureAwait(false);
        if (plan.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceReplacementDto>(plan.Error);
        }

        OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto> selected =
            await organizations.GetEnrollmentLinkAsync(
                new OrganizationJoinSourceLookupRequest(
                    context.Value.OrganizationId, sourceId, context.Value.ActorId),
                cancellationToken).ConfigureAwait(false);
        if (!selected.IsSuccess)
        {
            return ManagementFailure();
        }

        Result<OrganizationEnrollmentLinkDto> denied = await this.DenyEnrollmentLinkAsync(
            selected.Value!, expectedVersion, context.Value, cancellationToken).ConfigureAwait(false);
        if (denied.IsFailure)
        {
            return Result.Failure<WorkspaceStaffJoinSourceReplacementDto>(denied.Error);
        }

        Result<WorkspaceStaffJoinSourceIssuanceDto> replacement = await issuer.IssueEnrollmentLinkAsync(
            new WorkspaceEnrollmentLinkIssuanceRequest(
                replacementSourceId,
                lifetimeHours,
                selected.Value!.MaximumClaims,
                selected.Value.ApprovalMode,
                plan.Value.ProfileKey,
                plan.Value.Properties.Select(item => item.PropertyId).ToArray(),
                context.Value.ActorId),
            cancellationToken).ConfigureAwait(false);
        return replacement.IsFailure
            ? Result.Failure<WorkspaceStaffJoinSourceReplacementDto>(replacement.Error)
            : Result.Success(new WorkspaceStaffJoinSourceReplacementDto(
                sourceId, Map(denied.Value.Status), denied.Value.Version, replacement.Value));
    }

    private async Task<Result<OrganizationInvitationDto>> DenyInvitationAsync(
        OrganizationInvitationDto source,
        long expectedVersion,
        ReplacementContext context,
        CancellationToken cancellationToken)
    {
        if (source.Status == OrganizationInvitationStatus.Pending)
        {
            OrganizationJoinSourceOperation<OrganizationInvitationDto> revoked =
                await organizations.RevokeInvitationAsync(
                    new OrganizationInvitationRevocationRequest(
                        context.OrganizationId,
                        source.InvitationId,
                        expectedVersion,
                        context.ActorId,
                        context.ActorId),
                    cancellationToken).ConfigureAwait(false);
            return revoked.IsSuccess
                ? Result.Success(revoked.Value!)
                : Result.Failure<OrganizationInvitationDto>(
                    WorkspaceAccessManagementErrors.JoinSourceManagementFailed);
        }

        bool unusable = source.Status is OrganizationInvitationStatus.Revoked or
            OrganizationInvitationStatus.Superseded or OrganizationInvitationStatus.Expired;
        return unusable && VersionMatchesTerminalRetry(source.Version, expectedVersion)
            ? Result.Success(source)
            : Result.Failure<OrganizationInvitationDto>(
                WorkspaceAccessManagementErrors.JoinSourceReplacementUnavailable);
    }

    private async Task<Result<OrganizationEnrollmentLinkDto>> DenyEnrollmentLinkAsync(
        OrganizationEnrollmentLinkDto source,
        long expectedVersion,
        ReplacementContext context,
        CancellationToken cancellationToken)
    {
        if (source.Status is OrganizationEnrollmentLinkStatus.Active or
            OrganizationEnrollmentLinkStatus.CapacityReached)
        {
            OrganizationJoinSourceOperation<OrganizationEnrollmentLinkDto> disabled =
                await organizations.DisableEnrollmentLinkAsync(
                    new OrganizationEnrollmentLinkDisableRequest(
                        context.OrganizationId,
                        source.EnrollmentLinkId,
                        expectedVersion,
                        context.ActorId,
                        context.ActorId),
                    cancellationToken).ConfigureAwait(false);
            return disabled.IsSuccess
                ? Result.Success(disabled.Value!)
                : Result.Failure<OrganizationEnrollmentLinkDto>(
                    WorkspaceAccessManagementErrors.JoinSourceManagementFailed);
        }

        bool unusable = source.Status is OrganizationEnrollmentLinkStatus.Disabled or
            OrganizationEnrollmentLinkStatus.Rotated or OrganizationEnrollmentLinkStatus.Expired;
        return unusable && VersionMatchesTerminalRetry(source.Version, expectedVersion)
            ? Result.Success(source)
            : Result.Failure<OrganizationEnrollmentLinkDto>(
                WorkspaceAccessManagementErrors.JoinSourceReplacementUnavailable);
    }

    private async Task<Result<WorkspaceStaffAccessPlan>> GetPlanAsync(
        Guid sourceId,
        WorkspaceStaffOnboardingSource sourceKind,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(sourceId, cancellationToken)
            .ConfigureAwait(false);
        return plan is not null && plan.SourceKind == sourceKind
            ? Result.Success(plan)
            : Result.Failure<WorkspaceStaffAccessPlan>(
                WorkspaceAccessManagementErrors.JoinSourcePlanUnavailable);
    }

    private Result<ReplacementContext> Validate(
        Guid sourceId,
        Guid replacementSourceId,
        long expectedVersion,
        int lifetimeHours,
        string actorSubjectId)
    {
        string actor = actorSubjectId?.Trim() ?? string.Empty;
        if (!Guid.TryParse(scopeContext.ScopeId, out Guid organizationId) ||
            organizationId == Guid.Empty || sourceId == Guid.Empty ||
            replacementSourceId == Guid.Empty || replacementSourceId == sourceId ||
            expectedVersion <= 0 || lifetimeHours <= 0 || actor.Length == 0)
        {
            return Result.Failure<ReplacementContext>(
                WorkspaceAccessManagementErrors.JoinSourceRequestInvalid);
        }

        return Result.Success(new ReplacementContext(organizationId, actor));
    }

    private static bool VersionMatchesTerminalRetry(long currentVersion, long expectedVersion) =>
        currentVersion == expectedVersion ||
        (expectedVersion < long.MaxValue && currentVersion == expectedVersion + 1);

    private static WorkspaceStaffJoinSourceStatus Map(OrganizationInvitationStatus status) => status switch
    {
        OrganizationInvitationStatus.Revoked => WorkspaceStaffJoinSourceStatus.Revoked,
        OrganizationInvitationStatus.Superseded => WorkspaceStaffJoinSourceStatus.Superseded,
        OrganizationInvitationStatus.Expired => WorkspaceStaffJoinSourceStatus.Expired,
        _ => WorkspaceStaffJoinSourceStatus.Unknown
    };

    private static WorkspaceStaffJoinSourceStatus Map(OrganizationEnrollmentLinkStatus status) => status switch
    {
        OrganizationEnrollmentLinkStatus.Disabled => WorkspaceStaffJoinSourceStatus.Disabled,
        OrganizationEnrollmentLinkStatus.Rotated => WorkspaceStaffJoinSourceStatus.Superseded,
        OrganizationEnrollmentLinkStatus.Expired => WorkspaceStaffJoinSourceStatus.Expired,
        _ => WorkspaceStaffJoinSourceStatus.Unknown
    };

    private static Result<WorkspaceStaffJoinSourceReplacementDto> ManagementFailure() =>
        Result.Failure<WorkspaceStaffJoinSourceReplacementDto>(
            WorkspaceAccessManagementErrors.JoinSourceManagementFailed);

    private sealed record ReplacementContext(Guid OrganizationId, string ActorId);
}
