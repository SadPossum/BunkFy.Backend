namespace BunkFy.Modules.DataRights.Contracts;

public static class DataRightsAdminPermissionCodes
{
    public const string Read = DataRightsModuleMetadata.Name + ".read";
    public const string Create = DataRightsModuleMetadata.Name + ".create";
    public const string Discover = DataRightsModuleMetadata.Name + ".discover";
    public const string Review = DataRightsModuleMetadata.Name + ".review";
    public const string Decide = DataRightsModuleMetadata.Name + ".decide";
    public const string Execute = DataRightsModuleMetadata.Name + ".execute";
    public const string Export = DataRightsModuleMetadata.Name + ".export";
    public const string DownloadExport = DataRightsModuleMetadata.Name + ".export.download";
    public const string Restrict = DataRightsModuleMetadata.Name + ".restrict";
    public const string Erase = DataRightsModuleMetadata.Name + ".erase";
    public const string TerminateTenant = DataRightsModuleMetadata.Name + ".tenant-termination";
    public const string Manage = DataRightsModuleMetadata.Name + ".manage";
}
