namespace Inventory.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using Inventory.Contracts;
using Properties.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryProfileTests
{
    [Fact]
    public void Default_profile_documents_projection_runtime_dependencies()
    {
        ModuleProfileDescriptor profile = InventoryProfiles.Default;

        Assert.Equal(InventoryModuleMetadata.Name, profile.ModuleName);
        Assert.Contains(profile.Provides, feature => feature.Id == InventoryCompositionFeatures.Availability);
        Assert.Contains(profile.Requires, feature => feature.Id == TenancyCompositionFeatures.Context);
        Assert.Contains(profile.Requires, feature => feature.Id == PropertiesCompositionFeatures.PhysicalSetup);
        Assert.Contains(profile.Requires, feature => feature.Id == MessagingCompositionFeatures.Outbox);
        Assert.Contains(profile.Requires, feature => feature.Id == MessagingCompositionFeatures.NatsConsumers && feature.Optional);
        Assert.Contains(profile.Requires, feature => feature.Id == TasksCompositionFeatures.Worker && feature.Optional);
        Assert.Contains(profile.RequiredModules, module => module.ModuleName == PropertiesModuleMetadata.Name);
    }

    [Fact]
    public void Descriptor_exposes_scoped_permissions_event_task_and_profile()
    {
        Assert.Single(InventoryModuleMetadata.Descriptor.GetCompositionProfiles());
        IReadOnlyCollection<ModulePermissionDescriptor> permissions = InventoryModuleMetadata.Descriptor.GetPermissions();

        Assert.Equal(3, permissions.Count);
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeRequirement.Scoped, permission.ScopeRequirement));
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeGrantPolicy.Descendants, permission.ScopeGrantPolicy));
        Assert.Equal(8, InventoryModuleMetadata.Descriptor.GetPublishedEvents().Count);
        Assert.Single(InventoryModuleMetadata.Descriptor.GetTasks());
    }
}
