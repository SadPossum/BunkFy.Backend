namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;

public interface ITenantTerminationExportFragmentAssembler
{
    Task<TenantTerminationExportFragmentAssemblyResult> AssembleAsync(
        TenantTerminationExportFragmentAssemblyRequest request,
        Stream destination,
        CancellationToken cancellationToken);
}
