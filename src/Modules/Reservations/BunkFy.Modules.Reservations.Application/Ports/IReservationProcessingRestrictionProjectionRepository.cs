namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Domain.DataRights;

public interface IReservationProcessingRestrictionProjectionRepository
{
    Task<ReservationProcessingRestrictionProjection?> GetAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken);

    Task EnsureAsync(
        string tenantId,
        Guid propertyId,
        Guid reservationId,
        DateTimeOffset initializedAtUtc,
        CancellationToken cancellationToken);
}
