namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface ITenantTerminationExportArtifactRepository
{
    Task AddAsync(
        TenantTerminationExportArtifact artifact,
        CancellationToken cancellationToken);

    Task<TenantTerminationExportArtifact?> GetAsync(
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<TenantTerminationExportArtifact?> GetByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<TenantTerminationExportArtifact?> GetByProcessAsync(
        Guid processId,
        long exportOperationRevision,
        CancellationToken cancellationToken);
}
