namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;

public interface IDataRightsExportArtifactGenerator
{
    Task<DataRightsProtectedExportArtifact> GenerateAsync(
        DataRightsExportGenerationRequest request,
        CancellationToken cancellationToken);
}
