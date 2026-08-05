namespace BunkFy.Modules.DataRights.Domain.Models;

public sealed record TenantTerminationFrozenOwnerDescriptor(
    string OwnerKey,
    int ContractVersion,
    int CatalogVersion,
    string CatalogSha256);
