namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Microsoft.Extensions.Logging;

[IntegrationEventHandler(
    WorkspacesModuleMetadata.StaffOnboardingRestrictionRecoveryHandlerName)]
internal sealed class
    WorkspaceStaffOnboardingProcessingRestrictionRecoveryHandler(
        IWorkspaceStaffOnboardingRepository applications,
        WorkspaceStaffOnboardingProcessor processor,
        ILogger<
            WorkspaceStaffOnboardingProcessingRestrictionRecoveryHandler>
            logger)
    : IIntegrationEventHandler<
        WorkspaceStaffOnboardingProcessingRestrictionChangedIntegrationEvent>
{
    public async Task HandleAsync(
        WorkspaceStaffOnboardingProcessingRestrictionChangedIntegrationEvent
            integrationEvent,
        CancellationToken cancellationToken)
    {
        if (integrationEvent.IsRestricted ||
            integrationEvent.ContractVersion !=
                WorkspaceStaffOnboardingProcessingRestrictionContract
                    .CurrentVersion)
        {
            return;
        }

        WorkspaceStaffOnboarding? application = await applications.GetAsync(
            integrationEvent.ApplicationId,
            cancellationToken).ConfigureAwait(false);
        if (application is null ||
            application.Status != WorkspaceStaffOnboardingState.Provisioning ||
            application.StaffMemberId.HasValue)
        {
            return;
        }

        Result recovered = await processor.ProcessAsync(
            application,
            cancellationToken).ConfigureAwait(false);
        if (recovered.IsSuccess)
        {
            return;
        }

        if (recovered.Error ==
            WorkspaceStaffOnboardingApplicationErrors.ProcessingRestricted)
        {
            logger.LogInformation(
                "Staff onboarding restriction release recovery stopped because a newer restriction is active.");
            return;
        }

        throw new InvalidOperationException(
            $"Staff onboarding restriction release recovery failed with '{recovered.Error.Code}'.");
    }
}
