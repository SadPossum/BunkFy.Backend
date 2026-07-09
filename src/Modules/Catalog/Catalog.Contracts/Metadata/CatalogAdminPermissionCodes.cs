namespace Catalog.Contracts;

public static class CatalogAdminPermissionCodes
{
    public const string ItemsRead = CatalogModuleMetadata.Name + ".items.read";
    public const string ItemsCreate = CatalogModuleMetadata.Name + ".items.create";
    public const string ItemsUpdate = CatalogModuleMetadata.Name + ".items.update";
    public const string ItemsDiscontinue = CatalogModuleMetadata.Name + ".items.discontinue";
}
