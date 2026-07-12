namespace BunkFy.Modules.Staff.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;
using BunkFy.Modules.Staff.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffContractTests
{
    [Fact]
    public void Descriptor_exposes_scoped_permissions_property_subscriptions_and_rebuild_task()
    {
        IReadOnlyCollection<ModulePermissionDescriptor> permissions = StaffModuleMetadata.Descriptor.GetPermissions();
        Assert.Equal(5, permissions.Count);
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeRequirement.Scoped,
            permission.ScopeRequirement));
        Assert.Equal(3, StaffModuleMetadata.Descriptor.GetSubscriptions().Count);
        Assert.Equal(5, StaffModuleMetadata.Descriptor.GetPublishedEvents().Count);
        Assert.Single(StaffModuleMetadata.Descriptor.GetTasks());
        Assert.Single(StaffModuleMetadata.Descriptor.GetCompositionProfiles());
    }

    [Fact]
    public void Integration_events_are_pii_free_and_subjects_are_stable()
    {
        string[] forbidden = ["DisplayName", "LegalName", "WorkEmail", "WorkPhone",
            "EmployeeNumber", "JobTitle", "Department", "Reason"];
        Type[] eventTypes = [typeof(StaffMemberCreatedIntegrationEvent),
            typeof(StaffMemberUpdatedIntegrationEvent), typeof(StaffMemberLifecycleChangedIntegrationEvent),
            typeof(StaffAuthSubjectChangedIntegrationEvent), typeof(StaffPropertyAssignmentChangedIntegrationEvent)];
        Assert.All(eventTypes, eventType => Assert.DoesNotContain(eventType.GetProperties(),
            property => forbidden.Contains(property.Name, StringComparer.Ordinal)));
        Assert.EndsWith(".staff.member-created.v1", StaffIntegrationSubjects.CreateMemberCreated(),
            StringComparison.Ordinal);
        Assert.EndsWith(".staff.property-assignment-changed.v1",
            StaffIntegrationSubjects.CreatePropertyAssignmentChanged(), StringComparison.Ordinal);
    }
}
