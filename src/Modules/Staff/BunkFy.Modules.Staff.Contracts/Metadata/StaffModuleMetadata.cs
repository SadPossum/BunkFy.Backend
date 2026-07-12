namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Messaging;
using Gma.Framework.Modules;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;
using BunkFy.Modules.Properties.Contracts;

public static class StaffModuleMetadata
{
    public const string Name = "staff";
    public const string Schema = "staff";
    public const string PropertyCreatedHandlerName = "property-created";
    public const string PropertyUpdatedHandlerName = "property-updated";
    public const string PropertyRetiredHandlerName = "property-retired";
    public const string PropertiesProjectionName = "properties";
    public const int PropertiesProjectionVersion = 1;
    public const string ProjectionWorkerGroup = "projection-workers";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithPermissions([
            new ModulePermissionDescriptor(StaffAdminPermissionCodes.Read, "Read staff profiles.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(StaffAdminPermissionCodes.Create, "Create staff profiles.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(StaffAdminPermissionCodes.Manage, "Manage staff profiles.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(StaffAdminPermissionCodes.AssignProperties, "Manage staff property assignments.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(StaffAdminPermissionCodes.ManageLifecycle, "Manage staff employment lifecycle.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants)
        ])
        .WithSubscription<PropertyCreatedIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyCreatedHandlerName)
        .WithSubscription<PropertyUpdatedIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyUpdatedHandlerName)
        .WithSubscription<PropertyRetiredIntegrationEvent>(PropertiesModuleMetadata.Name, PropertyRetiredHandlerName)
        .WithPublishedEvent<StaffMemberCreatedIntegrationEvent>()
        .WithPublishedEvent<StaffMemberUpdatedIntegrationEvent>()
        .WithPublishedEvent<StaffMemberLifecycleChangedIntegrationEvent>()
        .WithPublishedEvent<StaffAuthSubjectChangedIntegrationEvent>()
        .WithPublishedEvent<StaffPropertyAssignmentChangedIntegrationEvent>()
        .WithTask<RebuildStaffPropertiesPayload>()
        .WithProfile(StaffProfiles.Default)
        .Build();
}
