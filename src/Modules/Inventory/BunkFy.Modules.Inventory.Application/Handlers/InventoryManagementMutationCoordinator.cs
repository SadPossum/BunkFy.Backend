namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using Gma.Framework.Scoping;

internal sealed class InventoryManagementMutationCoordinator(
    IInventoryManagementLock operationLock,
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

        return operationLock.AcquireResourceAsync(
            tenantId,
            InventoryManagementResourceKind.Room,
            roomId,
            cancellationToken);
    }

    public Task AcquireBlockAsync(
        Guid blockId,
        CancellationToken cancellationToken) => this.AcquireResourceAsync(
            InventoryManagementResourceKind.Block,
            blockId,
            cancellationToken);

    public Task AcquireBlockGroupAsync(
        Guid blockGroupId,
        CancellationToken cancellationToken) => this.AcquireResourceAsync(
            InventoryManagementResourceKind.BlockGroup,
            blockGroupId,
            cancellationToken);

    public Task AcquireBedRetirementAsync(
        Guid topologyChangeId,
        CancellationToken cancellationToken) => this.AcquireResourceAsync(
            InventoryManagementResourceKind.BedRetirement,
            topologyChangeId,
            cancellationToken);

    public Task AcquireRoomRetirementAsync(
        Guid topologyChangeId,
        CancellationToken cancellationToken) => this.AcquireResourceAsync(
            InventoryManagementResourceKind.RoomRetirement,
            topologyChangeId,
            cancellationToken);

    public Task AcquirePropertyOperationAsync(
        Guid propertyId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        string tenantId = this.GetTenantId(propertyId);
        if (operationId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "An Inventory management operation lock requires a valid operation id.");
        }

        return operationLock.AcquireOperationAsync(
            tenantId,
            InventoryManagementResourceKind.Property,
            propertyId,
            operationId,
            cancellationToken);
    }

    private Task AcquireResourceAsync(
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        CancellationToken cancellationToken) =>
        operationLock.AcquireResourceAsync(
            this.GetTenantId(resourceId),
            resourceKind,
            resourceId,
            cancellationToken);

    private string GetTenantId(Guid resourceId)
    {
        string tenantId = scopeContext.IsEnabled
            ? scopeContext.ScopeId?.Trim() ?? string.Empty
            : string.Empty;
        if (tenantId.Length == 0 || resourceId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "An Inventory management lock requires active tenant and resource coordinates.");
        }

        return tenantId;
    }
}
