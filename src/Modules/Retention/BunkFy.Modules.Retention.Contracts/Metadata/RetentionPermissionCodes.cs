namespace BunkFy.Modules.Retention.Contracts;

public static class RetentionPermissionCodes
{
    public const string Read = RetentionModuleMetadata.Name + ".read";
    public const string Manage = RetentionModuleMetadata.Name + ".manage";
    public const string Retry = RetentionModuleMetadata.Name + ".retry";
}
