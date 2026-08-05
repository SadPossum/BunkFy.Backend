namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

internal sealed class TenantTerminationExportArtifactRepository(
    DataRightsDbContext dbContext)
    : ITenantTerminationExportArtifactRepository
{
    public Task AddAsync(
        TenantTerminationExportArtifact artifact,
        CancellationToken cancellationToken)
    {
        dbContext.TenantTerminationExportArtifacts.Add(artifact);
        return Task.CompletedTask;
    }

    public Task<TenantTerminationExportArtifact?> GetAsync(
        Guid artifactId,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationExportArtifacts.SingleOrDefaultAsync(
            artifact => artifact.Id == artifactId,
            cancellationToken);

    public Task<TenantTerminationExportArtifact?> GetByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationExportArtifacts.SingleOrDefaultAsync(
            artifact => artifact.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<TenantTerminationExportArtifact?> GetByProcessAsync(
        Guid processId,
        long exportOperationRevision,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationExportArtifacts.SingleOrDefaultAsync(
            artifact =>
                artifact.ProcessId == processId &&
                artifact.ExportOperationRevision == exportOperationRevision,
            cancellationToken);
}
