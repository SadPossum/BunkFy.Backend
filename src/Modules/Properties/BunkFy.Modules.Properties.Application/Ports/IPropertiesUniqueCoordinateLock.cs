namespace BunkFy.Modules.Properties.Application.Ports;

public interface IPropertiesUniqueCoordinateLock
{
    Task AcquirePropertyCodeAsync(
        string tenantId,
        string propertyCode,
        CancellationToken cancellationToken);

    Task AcquireRoomNameAsync(
        string tenantId,
        Guid propertyId,
        string roomName,
        CancellationToken cancellationToken);
}
