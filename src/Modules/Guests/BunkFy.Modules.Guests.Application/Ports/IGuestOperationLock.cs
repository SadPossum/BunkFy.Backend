namespace BunkFy.Modules.Guests.Application.Ports;

public interface IGuestOperationLock
{
    Task AcquireGuestAsync(
        string tenantId,
        Guid guestId,
        CancellationToken cancellationToken);

    Task AcquirePropertiesAsync(
        string tenantId,
        IReadOnlyCollection<Guid> propertyIds,
        CancellationToken cancellationToken);
}
