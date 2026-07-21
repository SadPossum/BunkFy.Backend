namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Logging;

[IntegrationEventHandler(WorkspacesModuleMetadata.InvitationChangedHandlerName)]
internal sealed class OrganizationInvitationStaffOnboardingHandler(
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffAccessPlanRepository plans,
    WorkspaceStaffOnboardingProcessor processor,
    ISystemClock clock,
    ILogger<OrganizationInvitationStaffOnboardingHandler> logger)
    : IIntegrationEventHandler<OrganizationInvitationChangedIntegrationEvent>
{
    public async Task HandleAsync(
        OrganizationInvitationChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.Change == OrganizationInvitationChange.Accepted &&
            !string.IsNullOrWhiteSpace(integrationEvent.AcceptedSubjectId))
        {
            WorkspaceStaffOnboarding? application = await applications.GetBySourceAndSubjectAsync(
                WorkspaceStaffOnboardingSource.Invitation,
                integrationEvent.InvitationId,
                integrationEvent.AcceptedSubjectId,
                cancellationToken).ConfigureAwait(false);
            await ProcessWhenPresentAsync(application, processor, logger, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (integrationEvent.Status is OrganizationInvitationStatus.Revoked or
            OrganizationInvitationStatus.Superseded)
        {
            WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
                integrationEvent.InvitationId,
                cancellationToken).ConfigureAwait(false);
            plan?.Supersede(clock.UtcNow);
            IReadOnlyList<WorkspaceStaffOnboarding> active = await applications.ListActiveBySourceAsync(
                WorkspaceStaffOnboardingSource.Invitation,
                integrationEvent.InvitationId,
                cancellationToken).ConfigureAwait(false);
            foreach (WorkspaceStaffOnboarding application in active)
            {
                application.Supersede(clock.UtcNow);
            }
        }
    }

    private static async Task ProcessWhenPresentAsync(
        WorkspaceStaffOnboarding? application,
        WorkspaceStaffOnboardingProcessor processor,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (application is null)
        {
            logger.LogWarning("An accepted organization invitation had no BunkFy Staff onboarding application.");
            return;
        }

        Result result = await processor.ProcessAsync(application, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            logger.LogWarning(
                "Staff onboarding {ApplicationId} remains recoverable after {ErrorCode}.",
                application.Id,
                result.Error.Code);
        }
    }
}

[IntegrationEventHandler(WorkspacesModuleMetadata.EnrollmentClaimChangedHandlerName)]
internal sealed class OrganizationEnrollmentClaimStaffOnboardingHandler(
    IWorkspaceStaffOnboardingRepository applications,
    WorkspaceStaffOnboardingProcessor processor,
    ISystemClock clock,
    ILogger<OrganizationEnrollmentClaimStaffOnboardingHandler> logger)
    : IIntegrationEventHandler<OrganizationEnrollmentClaimChangedIntegrationEvent>
{
    public async Task HandleAsync(
        OrganizationEnrollmentClaimChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffOnboarding? application = await applications.GetBySourceAndSubjectAsync(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            integrationEvent.EnrollmentLinkId,
            integrationEvent.SubjectId,
            cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            logger.LogWarning(
                "Organization enrollment claim {ClaimId} had no BunkFy Staff onboarding application.",
                integrationEvent.ClaimId);
            return;
        }

        if (integrationEvent.Change == OrganizationEnrollmentClaimChange.Requested)
        {
            application.BindClaim(
                integrationEvent.ClaimId,
                integrationEvent.ClaimVersion,
                clock.UtcNow);
            return;
        }

        if (integrationEvent.Change == OrganizationEnrollmentClaimChange.Rejected)
        {
            application.Reject(integrationEvent.ClaimVersion, clock.UtcNow);
            return;
        }

        if (integrationEvent.Change == OrganizationEnrollmentClaimChange.Accepted)
        {
            if (!application.ClaimId.HasValue)
            {
                Result bound = application.BindClaim(
                    integrationEvent.ClaimId,
                    integrationEvent.ClaimVersion,
                    clock.UtcNow);
                if (bound.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Staff onboarding claim binding failed with '{bound.Error.Code}'.");
                }
            }

            Result processed = await processor.ProcessAsync(application, cancellationToken).ConfigureAwait(false);
            if (processed.IsFailure)
            {
                logger.LogWarning(
                    "Staff onboarding {ApplicationId} remains recoverable after {ErrorCode}.",
                    application.Id,
                    processed.Error.Code);
            }
        }
    }
}

[IntegrationEventHandler(WorkspacesModuleMetadata.EnrollmentLinkChangedHandlerName)]
internal sealed class OrganizationEnrollmentLinkStaffOnboardingHandler(
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffAccessPlanRepository plans,
    ISystemClock clock)
    : IIntegrationEventHandler<OrganizationEnrollmentLinkChangedIntegrationEvent>
{
    public async Task HandleAsync(
        OrganizationEnrollmentLinkChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.Status is not (OrganizationEnrollmentLinkStatus.Disabled or
            OrganizationEnrollmentLinkStatus.Rotated))
        {
            return;
        }

        IReadOnlyList<WorkspaceStaffOnboarding> active = await applications.ListActiveBySourceAsync(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            integrationEvent.EnrollmentLinkId,
            cancellationToken).ConfigureAwait(false);
        foreach (WorkspaceStaffOnboarding application in active)
        {
            application.Supersede(clock.UtcNow);
        }

        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
            integrationEvent.EnrollmentLinkId,
            cancellationToken).ConfigureAwait(false);
        plan?.Supersede(clock.UtcNow);
    }
}
