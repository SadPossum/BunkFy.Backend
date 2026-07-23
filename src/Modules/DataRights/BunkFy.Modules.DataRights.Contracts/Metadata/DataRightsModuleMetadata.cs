namespace BunkFy.Modules.DataRights.Contracts;

using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;
using Gma.Framework.Permissions;

public static class DataRightsModuleMetadata
{
    public const string Name = "data-rights";
    public const string Schema = "data-rights";

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
        .WithProfile(DataRightsProfiles.Default)
        .Build();

    private static ModulePermissionDescriptor Permission(string code, string description) => new(
        code,
        description,
        PermissionScopeRequirement.Scoped,
        PermissionScopeGrantPolicy.Descendants);
}
