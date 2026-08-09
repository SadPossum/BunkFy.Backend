namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;

[IntegrationEventHandler(WorkspacesModuleMetadata.EnrollmentClaimWithdrawnHandlerName)]
internal sealed class OrganizationEnrollmentClaimWithdrawnStaffOnboardingHandler(
    IWorkspaceStaffOnboardingRepository applications,
    IWorkspaceStaffAccessPlanRepository plans,
    WorkspaceStaffOnboardingMutationCoordinator mutations,
    ISystemClock clock)
    : IIntegrationEventHandler<OrganizationEnrollmentClaimWithdrawnIntegrationEvent>
{
    public async Task HandleAsync(
        OrganizationEnrollmentClaimWithdrawnIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        await mutations.AcquireSourceAsync(
                integrationEvent.EnrollmentLinkId,
                WorkspaceStaffOnboardingSourceLockMode.Write,
                cancellationToken).ConfigureAwait(false);
        WorkspaceStaffOnboarding? application = await applications.GetByClaimAsync(
            integrationEvent.ClaimId,
            cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            throw new InvalidOperationException(
                "A withdrawn organization enrollment claim had no BunkFy Staff onboarding application.");
        }

        if (!await mutations.AcquireTrackedUnderSourceAsync(
                application,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "A withdrawn organization enrollment claim lost its BunkFy Staff onboarding application.");
        }

        if (application.SourceKind != WorkspaceStaffOnboardingSource.EnrollmentLink ||
            application.SourceId != integrationEvent.EnrollmentLinkId)
        {
            throw new InvalidOperationException(
                "A withdrawn organization enrollment claim did not match its BunkFy Staff onboarding source.");
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result withdrawn = application.ObserveClaimWithdrawn(
            integrationEvent.ClaimId,
            integrationEvent.ClaimVersion,
            nowUtc);
        if (withdrawn.IsFailure)
        {
            throw new InvalidOperationException(
                $"Staff onboarding could not observe claim withdrawal: '{withdrawn.Error.Code}'.");
        }

        await OrganizationEnrollmentClaimExpiredStaffOnboardingHandler
            .ExpirePlanWhenUnusedUnderSourceLockAsync(
                applications,
                plans,
                application.SourceId,
                nowUtc,
                cancellationToken).ConfigureAwait(false);
    }
}
