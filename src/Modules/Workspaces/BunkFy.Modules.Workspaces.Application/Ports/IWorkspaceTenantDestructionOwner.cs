namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.DataRights.Contracts;

public interface IWorkspaceTenantDestructionOwner
{
    Task<TenantTerminationContributionResult> ExecuteAsync(
        TenantTerminationContributionRequest request,
        CancellationToken cancellationToken);
}
