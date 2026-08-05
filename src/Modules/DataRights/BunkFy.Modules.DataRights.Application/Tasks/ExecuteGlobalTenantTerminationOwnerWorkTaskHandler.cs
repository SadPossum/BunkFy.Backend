namespace BunkFy.Modules.DataRights.Application.Tasks;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Tasks;

internal sealed class ExecuteGlobalTenantTerminationOwnerWorkTaskHandler(
    ExecuteTenantTerminationOwnerWorkTaskHandler executor)
    : ITaskHandler<ExecuteGlobalTenantTerminationOwnerWorkPayload>
{
    public Task HandleAsync(
        ExecuteGlobalTenantTerminationOwnerWorkPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken) =>
        executor.HandleGlobalAsync(payload, context, cancellationToken);
}
