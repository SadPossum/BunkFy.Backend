namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Staff.Contracts;
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

    [Fact]
    public void Guest_data_hold_permission_is_delegable_but_not_seeded()
    {
        Assert.Contains(
            GuestsAdminPermissionCodes.DataHoldsManage,
            WorkspaceAccessRoles.DelegablePermissions);
        Assert.DoesNotContain(
            GuestsAdminPermissionCodes.DataHoldsManage,
            WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.All(WorkspaceAccessProfileSeeds.All, profile =>
            Assert.DoesNotContain(
                GuestsAdminPermissionCodes.DataHoldsManage,
                profile.Permissions));
    }

    [Theory]
    [InlineData(StaffAdminPermissionCodes.EmploymentGovernanceManage)]
    [InlineData(StaffAdminPermissionCodes.DataHoldsManage)]
    public void Staff_governance_permissions_are_delegable_sensitive_and_never_seeded(
        string permissionCode)
    {
        Assert.Contains(
            permissionCode,
            WorkspaceAccessRoles.DelegablePermissions);
        Assert.DoesNotContain(
            permissionCode,
            WorkspaceAccessRoles.LegacyMemberPermissions);
        Assert.DoesNotContain(
            permissionCode,
            WorkspaceAccessRoles.CompanySupportPermissionCeiling);
        Assert.All(WorkspaceAccessProfileSeeds.All, profile =>
            Assert.DoesNotContain(
                permissionCode,
                profile.Permissions));

        WorkspaceAccessPermissionDto permission = Assert.Single(
            WorkspaceAccessPermissionCatalogue.All,
            item => item.Code == permissionCode);
        Assert.True(permission.IsSensitive);
        Assert.Equal(
            [
                StaffAdminPermissionCodes.Read,
                StaffAdminPermissionCodes.SensitiveProfileRead
            ],
            permission.RequiredPermissions);
    }

    [Fact]
    public void Company_support_role_has_a_bounded_non_sensitive_permission_ceiling()
    {
        HashSet<string> delegable = WorkspaceAccessRoles.DelegablePermissions
            .ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<string> ceiling = WorkspaceAccessRoles.CompanySupportPermissionCeiling;

        Assert.NotEmpty(ceiling);
        Assert.Equal(ceiling.Count, ceiling.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(ceiling, permission => !delegable.Contains(permission));
        Assert.DoesNotContain(AccessControlPermissionGrants.OwnerWildcard, ceiling);
        Assert.DoesNotContain(StaffAdminPermissionCodes.SensitiveProfileRead, ceiling);
        Assert.DoesNotContain(IngestionAdminPermissionCodes.CredentialsManage, ceiling);
        Assert.DoesNotContain(IngestionAdminPermissionCodes.RawPayloadsRead, ceiling);
        Assert.DoesNotContain(IngestionAdminPermissionCodes.SensitiveHistoryRead, ceiling);
        Assert.DoesNotContain(RetentionPermissionCodes.Manage, ceiling);
        Assert.DoesNotContain(RetentionPermissionCodes.Retry, ceiling);
        Assert.DoesNotContain(
            ceiling,
            permission => permission.StartsWith(
                DataRightsModuleMetadata.Name + ".",
                StringComparison.Ordinal));
    }
}
