namespace BunkFy.Modules.DataRights.Tests.Contracts;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsModuleMetadataTests
{
    [Fact]
    public void Descriptor_exposes_scoped_permissions_and_one_tenant_profile()
    {
        IReadOnlyCollection<ModulePermissionDescriptor> permissions =
            DataRightsModuleMetadata.Descriptor.GetPermissions();

        Assert.Equal(12, permissions.Count);
        Assert.All(permissions, permission =>
        {
            Assert.Equal(PermissionScopeRequirement.Scoped, permission.ScopeRequirement);
            Assert.Equal(PermissionScopeGrantPolicy.Descendants, permission.ScopeGrantPolicy);
        });
        ModuleProfileDescriptor profile = Assert.Single(
            DataRightsModuleMetadata.Descriptor.GetCompositionProfiles());
        Assert.Equal(DataRightsProfiles.DefaultName, profile.ProfileName);
        Assert.Equal(5, DataRightsModuleMetadata.Descriptor.GetSubscriptions().Count);
        Assert.Contains(
            DataRightsModuleMetadata.Descriptor.GetSubscriptions(),
            subscription =>
                subscription.EventType ==
                    PropertyProcessingPolicyActivatedIntegrationEvent.EventType &&
                subscription.ProducerModule == PropertiesModuleMetadata.Name);
        Assert.Contains(
            DataRightsModuleMetadata.Descriptor.GetSubscriptions(),
            subscription =>
                subscription.EventType ==
                    PropertyProcessingSuspendedIntegrationEvent.EventType &&
                subscription.ProducerModule == PropertiesModuleMetadata.Name);
        Assert.Single(DataRightsModuleMetadata.Descriptor.GetTasks());
    }

    [Fact]
    public void Contract_and_domain_lifecycle_values_remain_cast_compatible()
    {
        Assert.Equal(
            Enum.GetValues<DataRightsOperation>().Select(value => (int)value),
            Enum.GetValues<DataRightsCaseOperation>().Select(value => (int)value));
        Assert.Equal(
            Enum.GetValues<DataRightsCaseStatus>().Select(value => (int)value),
            Enum.GetValues<DataRightsCaseState>().Select(value => (int)value));
        Assert.Equal(
            Enum.GetValues<DataRightsDecisionOutcome>().Select(value => (int)value),
            Enum.GetValues<DataRightsCaseDecision>().Select(value => (int)value));
        Assert.Equal(
            Enum.GetValues<DataRightsDecisionReason>().Select(value => (int)value),
            Enum.GetValues<DataRightsCaseDecisionReason>().Select(value => (int)value));
    }
}
