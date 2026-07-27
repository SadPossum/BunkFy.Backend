namespace BunkFy.Modules.Inventory.Application.Ports;

public interface IInventoryAllocationOperationLock
{
    Task AcquireAsync(
        string tenantId,
        Guid allocationId,
        CancellationToken cancellationToken);
}
