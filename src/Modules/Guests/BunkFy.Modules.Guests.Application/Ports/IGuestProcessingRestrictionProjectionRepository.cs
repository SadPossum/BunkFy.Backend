namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Domain.DataRights;

public interface IGuestProcessingRestrictionProjectionRepository
{
    Task<GuestProcessingRestrictionProjection?> GetAsync(
        Guid propertyId,
        Guid guestId,
        CancellationToken cancellationToken);

    Task EnsureAsync(
        string tenantId,
        Guid propertyId,
        Guid guestId,
        DateTimeOffset initializedAtUtc,
        CancellationToken cancellationToken);
}
