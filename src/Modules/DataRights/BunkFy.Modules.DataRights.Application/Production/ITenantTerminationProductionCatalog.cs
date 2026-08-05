namespace BunkFy.Modules.DataRights.Application.Production;

using Gma.Framework.Results;

public interface ITenantTerminationProductionCatalog
{
    Result<TenantTerminationProductionCatalogEvidence> Validate(
        IReadOnlyCollection<string> requiredOwnerKeys);
}

public sealed record TenantTerminationProductionCatalogEvidence(
    int OwnerCount,
    int ExportOwnerCount,
    string TerminalOwnerKey,
    string CatalogSha256);
