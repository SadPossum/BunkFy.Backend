namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Pagination;

public interface IReservationDetailsHistoryReader
{
    Task<ReservationDetailsOperationReplay?> FindOperationAsync(
        Guid propertyId,
        Guid reservationId,
        Guid correlationId,
        CancellationToken cancellationToken);

    Task<ReservationDetailsHistoryListResponse> ListAsync(
        Guid propertyId,
        Guid reservationId,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}

public sealed record ReservationDetailsOperationReplay(
    long ExpectedDetailsRevision,
    ReservationDetailsChangeOriginKind Origin,
    ReservationDetailsSnapshotDto After);
