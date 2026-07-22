namespace BunkFy.Modules.Workspaces.Tests;

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
}
