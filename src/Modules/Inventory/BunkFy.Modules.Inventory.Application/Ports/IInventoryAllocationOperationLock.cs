namespace BunkFy.Modules.Inventory.Application.Ports;

public interface IInventoryAllocationOperationLock
{
    Task<bool> TryAcquireExistingAsync(
        string tenantId,
        Guid allocationId,
        CancellationToken cancellationToken);

    Task AcquireCoordinateAsync(
        string tenantId,
        Guid allocationId,
        CancellationToken cancellationToken);
}
