namespace BunkFy.Modules.Guests.Application.Ports;

public interface IGuestProcessingRestrictionProjectionRepository
{
    Task EnsureAsync(
        string tenantId,
        Guid propertyId,
        Guid guestId,
        DateTimeOffset initializedAtUtc,
        CancellationToken cancellationToken);
}
