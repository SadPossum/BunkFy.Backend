namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;

[IntegrationEventHandler(WorkspacesModuleMetadata.InvitationExpiredHandlerName)]
internal sealed class OrganizationInvitationExpiredStaffOnboardingHandler(
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffAccessPlanRepository plans,
    ISystemClock clock)
    : IIntegrationEventHandler<OrganizationInvitationExpiredIntegrationEvent>
{
    public async Task HandleAsync(
        OrganizationInvitationExpiredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        DateTimeOffset nowUtc = clock.UtcNow;
        IReadOnlyList<WorkspaceStaffOnboarding> active = await applications.ListActiveBySourceAsync(
            WorkspaceStaffOnboardingSource.Invitation,
            integrationEvent.InvitationId,
            cancellationToken).ConfigureAwait(false);
        foreach (WorkspaceStaffOnboarding application in active)
        {
            EnsureObserved(application.Expire(nowUtc), "invitation expiry");
        }

        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
            integrationEvent.InvitationId,
            cancellationToken).ConfigureAwait(false);
        EnsureObserved(plan?.Expire(nowUtc) ?? Result.Success(), "invitation access-plan expiry");
    }

    private static void EnsureObserved(Result result, string observation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Staff onboarding could not observe {observation}: '{result.Error.Code}'.");
        }
    }
}

[IntegrationEventHandler(WorkspacesModuleMetadata.EnrollmentClaimExpiredHandlerName)]
internal sealed class OrganizationEnrollmentClaimExpiredStaffOnboardingHandler(
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffAccessPlanRepository plans,
    ISystemClock clock)
    : IIntegrationEventHandler<OrganizationEnrollmentClaimExpiredIntegrationEvent>
{
    public async Task HandleAsync(
        OrganizationEnrollmentClaimExpiredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffOnboarding? application = await applications.GetByClaimAsync(
            integrationEvent.ClaimId,
            cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            throw new InvalidOperationException(
                "An expired organization enrollment claim had no BunkFy Staff onboarding application.");
        }

        if (application.SourceKind != WorkspaceStaffOnboardingSource.EnrollmentLink ||
            application.SourceId != integrationEvent.EnrollmentLinkId)
        {
            throw new InvalidOperationException(
                "An expired organization enrollment claim did not match its BunkFy Staff onboarding source.");
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result expired = application.ObserveClaimExpired(
            integrationEvent.ClaimId,
            integrationEvent.ClaimVersion,
            nowUtc);
        EnsureObserved(expired, "claim expiry");

        await ExpirePlanWhenUnusedAsync(
            applications,
            plans,
            application.SourceId,
            nowUtc,
            cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureObserved(Result result, string observation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Staff onboarding could not observe {observation}: '{result.Error.Code}'.");
        }
    }

    internal static async Task ExpirePlanWhenUnusedAsync(
        IWorkspaceStaffOnboardingRepository applications,
        IWorkspaceStaffAccessPlanRepository plans,
        Guid enrollmentLinkId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkspaceStaffOnboarding> active = await applications.ListActiveBySourceAsync(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            enrollmentLinkId,
            cancellationToken).ConfigureAwait(false);
        if (active.Any(application => application.IsActive))
        {
            return;
        }

        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
            enrollmentLinkId,
            cancellationToken).ConfigureAwait(false);
        EnsureObserved(plan?.Expire(nowUtc) ?? Result.Success(), "enrollment access-plan expiry");
    }
}

[IntegrationEventHandler(WorkspacesModuleMetadata.EnrollmentLinkExpiredHandlerName)]
internal sealed class OrganizationEnrollmentLinkExpiredStaffOnboardingHandler(
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffAccessPlanRepository plans,
    ISystemClock clock)
    : IIntegrationEventHandler<OrganizationEnrollmentLinkExpiredIntegrationEvent>
{
    public Task HandleAsync(
        OrganizationEnrollmentLinkExpiredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        OrganizationEnrollmentClaimExpiredStaffOnboardingHandler.ExpirePlanWhenUnusedAsync(
            applications,
            plans,
            integrationEvent.EnrollmentLinkId,
            clock.UtcNow,
            cancellationToken);
}
