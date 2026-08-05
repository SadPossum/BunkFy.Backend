namespace BunkFy.Modules.DataRights.Contracts;

public interface ITenantTerminationProductionReadinessProbe
{
    Task<TenantTerminationProductionReadinessEvidence> CheckAsync(
        IReadOnlyCollection<string> requiredOwnerKeys,
        CancellationToken cancellationToken);
}

public interface ITenantTerminationRequiredOwnerCatalog
{
    IReadOnlyCollection<string> RequiredOwnerKeys { get; }
}

public sealed record TenantTerminationProductionReadinessEvidence(
    bool IsCatalogValid,
    int OwnerCount,
    int ExportOwnerCount,
    string TerminalOwnerKey,
    string CatalogSha256,
    string ReplayStoreProvider,
    bool IsReplayStoreReady,
    bool IsReplayStoreProductionGrade,
    string? ReplayStoreFailureCode);
