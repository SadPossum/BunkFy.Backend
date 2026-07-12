namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Guests.Contracts;

public interface IReservationGuestProfileProjectionRepository
{
    Task<bool> IsLinkableAsync(Guid propertyId, Guid guestId, CancellationToken cancellationToken);
    Task ApplyAsync(ReservationGuestProfileProjectionWriteModel profile, CancellationToken cancellationToken);
}

public sealed record ReservationGuestProfileProjectionWriteModel(
    string ScopeId,
    Guid GuestId,
    Guid? OriginPropertyId,
    GuestStatus Status,
    long Version);
