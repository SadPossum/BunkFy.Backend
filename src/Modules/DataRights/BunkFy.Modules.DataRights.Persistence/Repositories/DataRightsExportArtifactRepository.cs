namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsExportArtifactRepository(
    DataRightsDbContext dbContext)
    : IDataRightsExportArtifactRepository
{
    public Task AddAsync(
        DataRightsExportArtifact artifact,
        CancellationToken cancellationToken)
    {
        dbContext.ExportArtifacts.Add(artifact);
        return Task.CompletedTask;
    }

    public Task<DataRightsExportArtifact?> GetAsync(
        DataRightsCaseScope scope,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        ApplyScope(dbContext.ExportArtifacts, scope)
            .FirstOrDefaultAsync(
                artifact => artifact.Id == artifactId,
                cancellationToken);

    public Task<DataRightsExportArtifact?> GetByCaseAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        CancellationToken cancellationToken) =>
        ApplyScope(dbContext.ExportArtifacts, scope)
            .FirstOrDefaultAsync(
                artifact => artifact.CaseId == caseId,
                cancellationToken);

    public Task<DataRightsExportArtifact?> GetByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.ExportArtifacts.FirstOrDefaultAsync(
            artifact => artifact.IdempotencyKey == idempotencyKey,
            cancellationToken);

    private static IQueryable<DataRightsExportArtifact> ApplyScope(
        IQueryable<DataRightsExportArtifact> query,
        DataRightsCaseScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.IsTenant
            ? query.Where(artifact =>
                artifact.CaseKind == (DataRightsCaseKind)scope.CaseType &&
                artifact.PropertyId == null)
            : query.Where(artifact =>
                artifact.CaseKind == (DataRightsCaseKind)scope.CaseType &&
                artifact.PropertyId == scope.PropertyId!.Value);
    }
}
