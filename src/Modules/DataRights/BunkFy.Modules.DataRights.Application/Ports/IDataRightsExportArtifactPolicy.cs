namespace BunkFy.Modules.DataRights.Application.Ports;

public interface IDataRightsExportArtifactPolicy
{
    DateTimeOffset ExpiresAt(DateTimeOffset requestedAtUtc);
}
