namespace Properties.Contracts;

using Gma.Framework.Authorization;
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
            new ModulePermissionDescriptor(PropertiesAdminPermissionCodes.Read, "Read property setup.", tenantScoped: true),
            new ModulePermissionDescriptor(PropertiesAdminPermissionCodes.PropertiesManage, "Manage properties.", tenantScoped: true),
            new ModulePermissionDescriptor(PropertiesAdminPermissionCodes.RoomsManage, "Manage rooms.", tenantScoped: true),
            new ModulePermissionDescriptor(PropertiesAdminPermissionCodes.BedsManage, "Manage beds.", tenantScoped: true),
        ])
        .WithPublishedEvent<PropertyCreatedIntegrationEvent>()
        .WithPublishedEvent<PropertyUpdatedIntegrationEvent>()
        .WithPublishedEvent<RoomCreatedIntegrationEvent>()
        .WithPublishedEvent<RoomUpdatedIntegrationEvent>()
        .WithPublishedEvent<RoomRetiredIntegrationEvent>()
        .WithPublishedEvent<BedAddedIntegrationEvent>()
        .WithPublishedEvent<BedUpdatedIntegrationEvent>()
        .WithPublishedEvent<BedRetiredIntegrationEvent>()
        .WithProfile(PropertiesProfiles.Default)
        .Build();
}
