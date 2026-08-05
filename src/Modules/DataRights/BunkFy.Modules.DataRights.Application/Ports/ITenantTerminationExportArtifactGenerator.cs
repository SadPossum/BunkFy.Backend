namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface ITenantTerminationExportArtifactGenerator
{
    Task<TenantTerminationProtectedExportArtifact> GenerateAsync(
        TenantTerminationExportArtifact artifact,
        IReadOnlyCollection<TenantTerminationExportFragment> fragments,
        CancellationToken cancellationToken);
}
