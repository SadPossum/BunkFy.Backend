namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;

public interface IDataRightsExportAuditSink
{
    Task RecordAsync(
        DataRightsExportAuditFact fact,
        CancellationToken cancellationToken);
}
