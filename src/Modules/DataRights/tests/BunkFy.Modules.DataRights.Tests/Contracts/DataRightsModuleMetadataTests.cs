namespace BunkFy.Modules.DataRights.Tests.Contracts;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Permissions;
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
    }
}
