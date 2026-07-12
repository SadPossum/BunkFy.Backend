namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Permissions;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;

public static class PropertiesModuleMetadata
{
    public const string Name = "properties";
    public const string Schema = "properties";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithPermissions([
            new ModulePermissionDescriptor(PropertiesAdminPermissionCodes.Read, "Read property setup.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(PropertiesAdminPermissionCodes.PropertiesManage, "Manage properties.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(PropertiesAdminPermissionCodes.RoomsManage, "Manage rooms.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
            new ModulePermissionDescriptor(PropertiesAdminPermissionCodes.BedsManage, "Manage beds.", PermissionScopeRequirement.Scoped, PermissionScopeGrantPolicy.Descendants),
        ])
        .WithPublishedEvent<PropertyCreatedIntegrationEvent>()
        .WithPublishedEvent<PropertyUpdatedIntegrationEvent>()
        .WithPublishedEvent<PropertyRetiredIntegrationEvent>()
        .WithPublishedEvent<RoomCreatedIntegrationEvent>()
        .WithPublishedEvent<RoomUpdatedIntegrationEvent>()
        .WithPublishedEvent<RoomRetiredIntegrationEvent>()
        .WithPublishedEvent<BedAddedIntegrationEvent>()
        .WithPublishedEvent<BedUpdatedIntegrationEvent>()
        .WithPublishedEvent<BedRetiredIntegrationEvent>()
        .WithProfile(PropertiesProfiles.Default)
        .Build();
}
