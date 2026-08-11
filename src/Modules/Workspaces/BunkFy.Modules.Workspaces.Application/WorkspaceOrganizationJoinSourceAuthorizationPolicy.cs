namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.AccessControl;
using Gma.Framework.Permissions;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.DependencyInjection;

internal sealed class WorkspaceOrganizationJoinSourceAuthorizationPolicy(
    IWorkspaceAuthoritativeScope authoritativeScope)
    : IOrganizationJoinSourceAuthorizationPolicy
{
    public ValueTask<OrganizationJoinSourceAuthorizationDecision> EvaluateAsync(
        OrganizationJoinSourceAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ValueTask<OrganizationJoinSourceAuthorizationDecision>(
            authoritativeScope.RunAsync(
                context.OrganizationId,
                services => EvaluateScopedAsync(
                    services,
                    context,
                    cancellationToken)));
    }

    private static async Task<OrganizationJoinSourceAuthorizationDecision>
        EvaluateScopedAsync(
            IServiceProvider services,
            OrganizationJoinSourceAuthorizationContext context,
            CancellationToken cancellationToken)
    {
        WorkspaceOperationalAdmissionDecision operational = await services
            .GetRequiredService<WorkspaceOperationalAdmissionEvaluator>()
            .EvaluateAsync(
                context.OrganizationId.ToString("D"),
                cancellationToken).ConfigureAwait(false);
        if (operational.Outcome != WorkspaceOperationalAdmissionOutcome.Allowed)
        {
            return operational.Outcome ==
                WorkspaceOperationalAdmissionOutcome.Restricted
                ? OrganizationJoinSourceAuthorizationDecision.Denied
                : OrganizationJoinSourceAuthorizationDecision.Unavailable;
        }

        AccessSubject subject = AccessSubject.User(context.SubjectId);
        AccessScope scope = WorkspaceAccessScopes.Create(
            context.OrganizationId.ToString("D"));
        AccessRequirement[] requirements =
        [
            new AccessRequirement(
                subject,
                PermissionCode.Create(
                    WorkspacesPermissionCodes.StaffOnboardingManage),
                scope),
            new AccessRequirement(
                subject,
                PermissionCode.Create(
                    AccessControlProfilePermissionCodes.Read),
                scope)
        ];
        IReadOnlyList<AccessDecision> access = await services
            .GetRequiredService<IAccessAuthorizationService>()
            .AuthorizeManyAsync(requirements, cancellationToken)
            .ConfigureAwait(false);
        if (access.Count != requirements.Length ||
            access.Any(decision => !decision.IsAllowed))
        {
            return OrganizationJoinSourceAuthorizationDecision.Denied;
        }

        return context.Operation switch
        {
            OrganizationJoinSourceAuthorizationOperation.ReadInvitations =>
                await AuthorizeSourceReadAsync(
                    services,
                    context.SourceId,
                    WorkspaceStaffOnboardingSource.Invitation,
                    cancellationToken).ConfigureAwait(false),
            OrganizationJoinSourceAuthorizationOperation.IssueInvitation =>
                await AuthorizeActivePlanAsync(
                    services,
                    context.SourceId,
                    WorkspaceStaffOnboardingSource.Invitation,
                    cancellationToken).ConfigureAwait(false),
            OrganizationJoinSourceAuthorizationOperation.RevokeInvitation =>
                await AuthorizeExistingPlanAsync(
                    services,
                    context.SourceId,
                    WorkspaceStaffOnboardingSource.Invitation,
                    cancellationToken).ConfigureAwait(false),
            OrganizationJoinSourceAuthorizationOperation.ReadEnrollmentLinks =>
                await AuthorizeSourceReadAsync(
                    services,
                    context.SourceId,
                    WorkspaceStaffOnboardingSource.EnrollmentLink,
                    cancellationToken).ConfigureAwait(false),
            OrganizationJoinSourceAuthorizationOperation.IssueEnrollmentLink =>
                await AuthorizeActivePlanAsync(
                    services,
                    context.SourceId,
                    WorkspaceStaffOnboardingSource.EnrollmentLink,
                    cancellationToken).ConfigureAwait(false),
            OrganizationJoinSourceAuthorizationOperation.DisableEnrollmentLink =>
                await AuthorizeExistingPlanAsync(
                    services,
                    context.SourceId,
                    WorkspaceStaffOnboardingSource.EnrollmentLink,
                    cancellationToken).ConfigureAwait(false),
            OrganizationJoinSourceAuthorizationOperation.ResolveJoinRequest =>
                await AuthorizeClaimAsync(
                    services,
                    context.ClaimId,
                    cancellationToken).ConfigureAwait(false),
            _ => OrganizationJoinSourceAuthorizationDecision.NotApplicable
        };
    }

    private static Task<OrganizationJoinSourceAuthorizationDecision>
        AuthorizeSourceReadAsync(
            IServiceProvider services,
            Guid? sourceId,
            WorkspaceStaffOnboardingSource sourceKind,
            CancellationToken cancellationToken) =>
        !sourceId.HasValue
            ? Task.FromResult(
                OrganizationJoinSourceAuthorizationDecision.Allowed)
            : AuthorizeExistingPlanAsync(
                services,
                sourceId,
                sourceKind,
                cancellationToken);

    private static async Task<OrganizationJoinSourceAuthorizationDecision>
        AuthorizeActivePlanAsync(
            IServiceProvider services,
            Guid? sourceId,
            WorkspaceStaffOnboardingSource sourceKind,
            CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessPlan? plan = await GetPlanAsync(
            services,
            sourceId,
            cancellationToken).ConfigureAwait(false);
        return plan is not null &&
               plan.SourceKind == sourceKind &&
               plan.Status == WorkspaceStaffAccessPlanState.Active
            ? OrganizationJoinSourceAuthorizationDecision.Allowed
            : OrganizationJoinSourceAuthorizationDecision.Denied;
    }

    private static async Task<OrganizationJoinSourceAuthorizationDecision>
        AuthorizeExistingPlanAsync(
            IServiceProvider services,
            Guid? sourceId,
            WorkspaceStaffOnboardingSource sourceKind,
            CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessPlan? plan = await GetPlanAsync(
            services,
            sourceId,
            cancellationToken).ConfigureAwait(false);
        return plan?.SourceKind == sourceKind
            ? OrganizationJoinSourceAuthorizationDecision.Allowed
            : OrganizationJoinSourceAuthorizationDecision.Denied;
    }

    private static async Task<OrganizationJoinSourceAuthorizationDecision>
        AuthorizeClaimAsync(
            IServiceProvider services,
            Guid? claimId,
            CancellationToken cancellationToken)
    {
        if (!claimId.HasValue || claimId.Value == Guid.Empty)
        {
            return OrganizationJoinSourceAuthorizationDecision.Denied;
        }

        WorkspaceStaffOnboarding? application = await services
            .GetRequiredService<IWorkspaceStaffOnboardingRepository>()
            .GetByClaimAsync(claimId.Value, cancellationToken)
            .ConfigureAwait(false);
        return application is
        {
            SourceKind: WorkspaceStaffOnboardingSource.EnrollmentLink,
            Status: WorkspaceStaffOnboardingState.PendingApproval
        }
            ? OrganizationJoinSourceAuthorizationDecision.Allowed
            : OrganizationJoinSourceAuthorizationDecision.Denied;
    }

    private static Task<WorkspaceStaffAccessPlan?> GetPlanAsync(
        IServiceProvider services,
        Guid? sourceId,
        CancellationToken cancellationToken) =>
        !sourceId.HasValue || sourceId.Value == Guid.Empty
            ? Task.FromResult<WorkspaceStaffAccessPlan?>(null)
            : services.GetRequiredService<IWorkspaceStaffAccessPlanRepository>()
                .GetAsync(sourceId.Value, cancellationToken);
}
