namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;

public interface ITenantTerminationExportFragmentGenerator
{
    Task<TenantTerminationProtectedExportFragment> GenerateAsync(
        TenantTerminationExportFragmentGenerationRequest request,
        CancellationToken cancellationToken);
}
