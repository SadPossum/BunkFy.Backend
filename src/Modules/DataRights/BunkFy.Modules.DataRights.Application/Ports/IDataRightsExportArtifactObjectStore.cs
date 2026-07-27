namespace BunkFy.Modules.DataRights.Application.Ports;

public interface IDataRightsExportArtifactObjectStore
{
    Task<bool> DeleteAsync(
        Guid artifactId,
        CancellationToken cancellationToken);
}
