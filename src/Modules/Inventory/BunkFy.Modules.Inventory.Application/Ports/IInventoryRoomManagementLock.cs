namespace BunkFy.Modules.Inventory.Application.Ports;

public interface IInventoryRoomManagementLock
{
    Task AcquireAsync(
        string tenantId,
        Guid roomId,
        CancellationToken cancellationToken);
}
