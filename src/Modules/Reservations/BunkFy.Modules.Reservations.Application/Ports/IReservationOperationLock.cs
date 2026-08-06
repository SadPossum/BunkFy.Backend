namespace BunkFy.Modules.Reservations.Application.Ports;

public interface IReservationOperationLock
{
    Task<bool> TryAcquireExistingAsync(
        string tenantId,
        Guid reservationId,
        CancellationToken cancellationToken);

    Task AcquireCoordinateAsync(
        string tenantId,
        Guid reservationId,
        CancellationToken cancellationToken);
}
