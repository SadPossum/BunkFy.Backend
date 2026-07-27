namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;

public interface IDataRightsExportAssembler
{
    Task<DataRightsExportAssemblyResult> AssembleAsync(
        DataRightsExportGenerationRequest request,
        Stream destination,
        CancellationToken cancellationToken);
}
