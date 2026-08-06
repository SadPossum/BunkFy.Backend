namespace BunkFy.Modules.Inventory.Application.Ports;

public interface IInventoryAllocationRequestLock
{
    Task AcquireAsync(
        string tenantId,
        Guid allocationRequestId,
        Guid reservationId,
        CancellationToken cancellationToken);
}
