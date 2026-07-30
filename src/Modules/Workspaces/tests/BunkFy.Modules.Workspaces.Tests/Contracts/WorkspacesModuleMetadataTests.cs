namespace BunkFy.Modules.Workspaces.Tests.Contracts;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspacesModuleMetadataTests
{
    [Fact]
    public void Descriptor_publishes_tenant_correction_completion()
    {
        Assert.Contains(
            WorkspacesModuleMetadata.Descriptor.GetPublishedEvents(),
            published =>
                published.EventType ==
                DataRightsTenantCorrectionAppliedIntegrationEvent.EventType);
        Assert.Contains(
            Assert.Single(
                WorkspacesModuleMetadata.Descriptor
                    .GetCompositionProfiles()).Requires,
            feature =>
                feature.Id ==
                MessagingCompositionFeatures.Outbox);
    }
}
