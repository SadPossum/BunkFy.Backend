namespace BunkFy.Modules.Properties.Application.Ports;

public interface IPropertiesOperationLock
{
    Task<bool> TryAcquirePropertyAsync(
        string tenantId,
        Guid propertyId,
        CancellationToken cancellationToken);

    Task<bool> TryAcquireRoomAsync(
        string tenantId,
        Guid roomId,
        CancellationToken cancellationToken);
}
