namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using Gma.Framework.Scoping;

internal sealed class InventoryManagementMutationCoordinator(
    IInventoryRoomManagementLock operationLock,
    IScopeContext scopeContext)
{
    public Task AcquireRoomAsync(
        Guid roomId,
        CancellationToken cancellationToken)
    {
        string tenantId = scopeContext.IsEnabled
            ? scopeContext.ScopeId?.Trim() ?? string.Empty
            : string.Empty;
        if (tenantId.Length == 0 || roomId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "An Inventory management lock requires active tenant and Room coordinates.");
        }

        return operationLock.AcquireAsync(
            tenantId,
            roomId,
            cancellationToken);
    }
}
