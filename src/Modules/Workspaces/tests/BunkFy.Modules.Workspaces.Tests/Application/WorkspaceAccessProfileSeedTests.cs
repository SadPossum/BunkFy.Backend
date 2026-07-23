namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Modules.AccessControl.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceAccessProfileSeedTests
{
    [Fact]
    public void Membership_marker_grants_no_permissions()
    {
        Assert.Empty(WorkspaceAccessRoles.MembershipMarkerPermissions);
    }

    [Fact]
    public void Internal_provisioner_has_exactly_the_delegable_product_permissions()
    {
        Assert.Equal(
            WorkspaceAccessRoles.DelegablePermissions,
            WorkspaceAccessRoles.ProvisionerPermissions);
        Assert.DoesNotContain(
            AccessControlPermissionGrants.OwnerWildcard,
            WorkspaceAccessRoles.ProvisionerPermissions);
    }

    [Fact]
    public void Seed_profiles_are_unique_delegable_and_never_owner_profiles()
    {
        string[] keys = WorkspaceAccessProfileSeeds.All.Select(profile => profile.Key).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());

        HashSet<string> delegable = WorkspaceAccessRoles.DelegablePermissions.ToHashSet(StringComparer.Ordinal);
        Assert.All(WorkspaceAccessProfileSeeds.All, profile =>
        {
            Assert.DoesNotContain(AccessControlPermissionGrants.OwnerWildcard, profile.Permissions);
            Assert.DoesNotContain(profile.Permissions, permission => !delegable.Contains(permission));
            Assert.Equal(
                profile.Permissions.Count,
                profile.Permissions.Distinct(StringComparer.Ordinal).Count());
        });
    }

    [Fact]
    public void Front_desk_seed_preserves_the_legacy_operational_baseline()
    {
        Assert.Equal(
            WorkspaceAccessRoles.LegacyMemberPermissions.ToArray(),
            WorkspaceAccessProfileSeeds.FrontDesk.Permissions.ToArray());
    }

    [Fact]
    public void Data_rights_permissions_are_never_granted_to_operational_seed_profiles()
    {
        HashSet<string> dataRightsPermissions = WorkspaceAccessPermissionCatalogue.All
            .Where(permission => permission.Code.StartsWith(
                DataRightsModuleMetadata.Name + ".",
                StringComparison.Ordinal))
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(12, dataRightsPermissions.Count);
        Assert.DoesNotContain(
            WorkspaceAccessRoles.LegacyMemberPermissions,
            dataRightsPermissions.Contains);
        Assert.All(WorkspaceAccessProfileSeeds.All, profile =>
            Assert.DoesNotContain(profile.Permissions, dataRightsPermissions.Contains));
    }
}
