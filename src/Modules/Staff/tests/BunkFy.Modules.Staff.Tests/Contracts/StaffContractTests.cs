namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Admin.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Governance;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffContractTests
{
    [Fact]
    public void Employment_governance_contract_version_matches_domain_state()
    {
        Assert.Equal(
            StaffEmploymentGovernance.ContractVersion,
            StaffEmploymentGovernanceContract.CurrentVersion);
    }

    [Fact]
    public void Descriptor_exposes_scoped_permissions_property_subscriptions_and_rebuild_task()
    {
        IReadOnlyCollection<ModulePermissionDescriptor> permissions = StaffModuleMetadata.Descriptor.GetPermissions();
        Assert.Equal(9, permissions.Count);
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeRequirement.Scoped,
            permission.ScopeRequirement));
        Assert.Contains(permissions, permission =>
            permission.Code == StaffAdminPermissionCodes.SensitiveProfileRead);
        Assert.Contains(permissions, permission =>
            permission.Code == StaffAdminPermissionCodes.AccountLinksManage);
        Assert.Contains(permissions, permission =>
            permission.Code ==
                StaffAdminPermissionCodes.EmploymentGovernanceManage);
        Assert.Contains(permissions, permission =>
            permission.Code ==
                StaffAdminPermissionCodes.DataHoldsManage);
        Assert.Equal(3, StaffModuleMetadata.Descriptor.GetSubscriptions().Count);
        Assert.Equal(8, StaffModuleMetadata.Descriptor.GetPublishedEvents().Count);
        Assert.Contains(
            StaffModuleMetadata.Descriptor.GetPublishedEvents(),
            published =>
                published.EventType ==
                    DataRightsTenantCorrectionAppliedIntegrationEvent.EventType);
        Assert.Single(StaffModuleMetadata.Descriptor.GetTasks());
        Assert.Single(StaffModuleMetadata.Descriptor.GetCompositionProfiles());
        Assert.Equal(
            StaffAdminPermissionCodes.AccountLinksManage,
            StaffAdminPermissions.AccountLinksManage.Code);
    }

    [Fact]
    public void Integration_events_are_pii_free_and_subjects_are_stable()
    {
        string[] forbidden = ["DisplayName", "LegalName", "WorkEmail", "WorkPhone",
            "EmployeeNumber", "JobTitle", "Department", "Reason"];
        Type[] eventTypes = [typeof(StaffMemberCreatedIntegrationEvent),
            typeof(StaffMemberUpdatedIntegrationEvent), typeof(StaffMemberLifecycleChangedIntegrationEvent),
            typeof(StaffMemberAnonymisedIntegrationEvent),
            typeof(StaffAuthSubjectChangedIntegrationEvent),
            typeof(StaffPropertyAssignmentChangedIntegrationEvent),
            typeof(StaffProcessingRestrictionChangedIntegrationEvent),
            typeof(DataRightsTenantCorrectionAppliedIntegrationEvent)];
        Assert.All(eventTypes, eventType => Assert.DoesNotContain(eventType.GetProperties(),
            property => forbidden.Contains(property.Name, StringComparer.Ordinal)));
        Assert.EndsWith(".staff.member-created.v1", StaffIntegrationSubjects.CreateMemberCreated(),
            StringComparison.Ordinal);
        Assert.EndsWith(".staff.member-anonymised.v1",
            StaffIntegrationSubjects.CreateMemberAnonymised(), StringComparison.Ordinal);
        Assert.EndsWith(".staff.property-assignment-changed.v1",
            StaffIntegrationSubjects.CreatePropertyAssignmentChanged(), StringComparison.Ordinal);
    }
}
