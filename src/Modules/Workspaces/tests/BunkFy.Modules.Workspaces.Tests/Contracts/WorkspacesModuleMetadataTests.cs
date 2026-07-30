namespace BunkFy.Modules.Workspaces.Tests.Contracts;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Contributors;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspacesModuleMetadataTests
{
    [Fact]
    public void Descriptor_publishes_rights_completion_and_recovers_releases()
    {
        Assert.Contains(
            WorkspacesModuleMetadata.Descriptor.GetPublishedEvents(),
            published =>
                published.EventType ==
                DataRightsTenantCorrectionAppliedIntegrationEvent.EventType);
        Assert.Contains(
            WorkspacesModuleMetadata.Descriptor.GetPublishedEvents(),
            published =>
                published.EventType ==
                WorkspaceStaffOnboardingProcessingRestrictionChangedIntegrationEvent
                    .EventType);
        Assert.Contains(
            WorkspacesModuleMetadata.Descriptor.GetSubscriptions(),
            subscription =>
                subscription.ProducerModule ==
                    WorkspacesModuleMetadata.Name &&
                subscription.EventType ==
                    WorkspaceStaffOnboardingProcessingRestrictionChangedIntegrationEvent
                        .EventType &&
                subscription.HandlerName ==
                    WorkspacesModuleMetadata
                        .StaffOnboardingRestrictionRecoveryHandlerName);
        Assert.Contains(
            Assert.Single(
                WorkspacesModuleMetadata.Descriptor
                    .GetCompositionProfiles()).Requires,
            feature =>
                feature.Id ==
                MessagingCompositionFeatures.Outbox);
    }

    [Fact]
    public void Application_registers_one_workspaces_restriction_contributor()
    {
        ServiceCollection services = new();
        IConfiguration configuration =
            new ConfigurationBuilder().Build();

        services.AddWorkspacesApplication(configuration, "global");
        services.AddWorkspacesApplication(configuration, "global");

        ServiceDescriptor registration = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(IDataRightsRestrictionContributor));
        Assert.Equal(
            typeof(
                WorkspaceStaffOnboardingDataRightsRestrictionContributor),
            registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
    }
}
