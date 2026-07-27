namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface IDataRightsExportArtifactReader
{
    Task<DataRightsExportDownload> OpenVerifiedAsync(
        DataRightsExportArtifact artifact,
        CancellationToken cancellationToken);
}
