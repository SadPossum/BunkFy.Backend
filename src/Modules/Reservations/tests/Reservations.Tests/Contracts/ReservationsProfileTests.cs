namespace Reservations.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.Permissions;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Inventory.Contracts;
using Reservations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationsProfileTests
{
    [Fact]
    public void Default_profile_requires_inventory_and_outbox()
    {
        Assert.Contains(
            ReservationsProfiles.Default.Requires,
            feature => feature.Id == InventoryCompositionFeatures.Availability);
        Assert.Contains(
            ReservationsProfiles.Default.Requires,
            feature => feature.Id == MessagingCompositionFeatures.Outbox);
        Assert.Contains(
            ReservationsProfiles.Default.RequiredModules,
            module => module.ModuleName == InventoryModuleMetadata.Name);
    }

    [Fact]
    public void Descriptor_exposes_scoped_permissions_and_saga_contracts()
    {
        IReadOnlyCollection<ModulePermissionDescriptor> permissions = ReservationsModuleMetadata.Descriptor.GetPermissions();

        Assert.Equal(4, permissions.Count);
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeRequirement.Scoped, permission.ScopeRequirement));
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeGrantPolicy.Descendants, permission.ScopeGrantPolicy));
        Assert.Equal(6, ReservationsModuleMetadata.Descriptor.GetPublishedEvents().Count);
        Assert.Equal(7, ReservationsModuleMetadata.Descriptor.GetSubscriptions().Count);
        Assert.Single(ReservationsModuleMetadata.Descriptor.GetTasks());
        Assert.Single(ReservationsModuleMetadata.Descriptor.GetCompositionProfiles());
    }
}
