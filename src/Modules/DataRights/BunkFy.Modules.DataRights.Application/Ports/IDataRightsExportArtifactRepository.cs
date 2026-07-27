namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface IDataRightsExportArtifactRepository
{
    Task AddAsync(
        DataRightsExportArtifact artifact,
        CancellationToken cancellationToken);

    Task<DataRightsExportArtifact?> GetAsync(
        DataRightsCaseScope scope,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<DataRightsExportArtifact?> GetByCaseAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        CancellationToken cancellationToken);

    Task<DataRightsExportArtifact?> GetByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);
}
