namespace BunkFy.Modules.DataRights.Tests.Contracts;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Models;
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
