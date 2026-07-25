namespace BunkFy.Modules.Reservations.Application.Ports;

public interface IReservationOperationLock
{
    Task AcquireAsync(
        string tenantId,
        Guid reservationId,
        CancellationToken cancellationToken);
}
