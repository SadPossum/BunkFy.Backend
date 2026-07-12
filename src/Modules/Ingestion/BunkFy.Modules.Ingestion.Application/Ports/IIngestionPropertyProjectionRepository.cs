namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface IIngestionPropertyProjectionRepository
{
    Task ApplyAsync(IngestionPropertyProjectionWriteModel property, CancellationToken cancellationToken);
    Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken);
}

public sealed record IngestionPropertyProjectionWriteModel(
    string ScopeId,
    Guid PropertyId,
    string? Name,
    string? Code,
    bool IsActive,
    long SourceVersion);
