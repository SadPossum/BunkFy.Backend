namespace BunkFy.Modules.Inventory.Application.Ports;

public interface IInventoryManagementLock
{
    Task AcquireResourceAsync(
        string tenantId,
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        CancellationToken cancellationToken);

    Task AcquireOperationAsync(
        string tenantId,
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        Guid operationId,
        CancellationToken cancellationToken);
}
