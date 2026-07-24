namespace BunkFy.Modules.DataRights.Contracts;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;
using Gma.Framework.Permissions;
using Gma.Framework.Tasks;

public static class DataRightsModuleMetadata
{
    public const string Name = "data-rights";
    public const string Schema = "data-rights";
    public const string PropertyCreatedHandlerName = "property-created";
    public const string PropertyUpdatedHandlerName = "property-updated";
    public const string PropertyRetiredHandlerName = "property-retired";
    public const string PropertyProcessingPolicyActivatedHandlerName =
        "property-processing-policy-activated";
    public const string PropertyProcessingSuspendedHandlerName =
        "property-processing-suspended";
    public const string PropertiesProjectionName = "properties";
    public const int PropertiesProjectionVersion = 1;
    public const string ProjectionWorkerGroup = "projection-workers";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithPermissions([
            Permission(DataRightsAdminPermissionCodes.Read, "Read data-rights cases."),
            Permission(DataRightsAdminPermissionCodes.Create, "Create data-rights cases."),
            Permission(DataRightsAdminPermissionCodes.Discover, "Run sensitive record discovery."),
            Permission(DataRightsAdminPermissionCodes.Review, "Review discovered records and case scope."),
            Permission(DataRightsAdminPermissionCodes.Decide, "Approve or deny data-rights operations."),
            Permission(DataRightsAdminPermissionCodes.Execute, "Execute approved data-rights work."),
            Permission(DataRightsAdminPermissionCodes.Export, "Generate protected data exports."),
            Permission(DataRightsAdminPermissionCodes.DownloadExport, "Download protected data exports."),
            Permission(DataRightsAdminPermissionCodes.Restrict, "Apply or release processing restrictions."),
            Permission(DataRightsAdminPermissionCodes.Erase, "Execute erasure or anonymisation."),
            Permission(DataRightsAdminPermissionCodes.TerminateTenant, "Execute tenant termination."),
            Permission(DataRightsAdminPermissionCodes.Manage, "Manage data-rights case lifecycle.")
        ])
        .WithSubscription<PropertyCreatedIntegrationEvent>(
            PropertiesModuleMetadata.Name,
            PropertyCreatedHandlerName)
        .WithSubscription<PropertyUpdatedIntegrationEvent>(
            PropertiesModuleMetadata.Name,
            PropertyUpdatedHandlerName)
        .WithSubscription<PropertyRetiredIntegrationEvent>(
            PropertiesModuleMetadata.Name,
            PropertyRetiredHandlerName)
        .WithSubscription<PropertyProcessingPolicyActivatedIntegrationEvent>(
            PropertiesModuleMetadata.Name,
            PropertyProcessingPolicyActivatedHandlerName)
        .WithSubscription<PropertyProcessingSuspendedIntegrationEvent>(
            PropertiesModuleMetadata.Name,
            PropertyProcessingSuspendedHandlerName)
        .WithTask<RebuildDataRightsPropertiesPayload>()
        .WithProfile(DataRightsProfiles.Default)
        .Build();

    private static ModulePermissionDescriptor Permission(string code, string description) => new(
        code,
        description,
        PermissionScopeRequirement.Scoped,
        PermissionScopeGrantPolicy.Descendants);
}
