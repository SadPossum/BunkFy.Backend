namespace Catalog.Admin.Contracts;

using Catalog.Contracts;

public static class CatalogAdminOperationNames
{
    public const string ItemsList = CatalogModuleMetadata.Name + ".items.list";
    public const string ItemsGet = CatalogModuleMetadata.Name + ".items.get";
    public const string ItemsCreate = CatalogModuleMetadata.Name + ".items.create";
    public const string ItemsUpdate = CatalogModuleMetadata.Name + ".items.update";
    public const string ItemsDiscontinue = CatalogModuleMetadata.Name + ".items.discontinue";
}
