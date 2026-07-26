namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;

public interface IReservationAnonymisationRestoreRepository
{
    Task<Reservation?> GetReservationAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<ReservationAnonymisationReceipt?> GetOriginalReceiptAsync(
        Guid receiptId,
        CancellationToken cancellationToken);

    Task<ReservationAnonymisationTombstone?> GetTombstoneAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<ReservationAnonymisationRestoreReceipt?> GetRestoreReceiptAsync(
        Guid ledgerEntryId,
        CancellationToken cancellationToken);

    Task<ReservationAnonymisationAffectedRecords>
        RedactRestoredOwnedRecordsAsync(
            Reservation reservation,
            ReservationAnonymisationRestoreOutcome? outcome,
            CancellationToken cancellationToken);

    Task<bool> VerifyRestoredOwnerStateAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken);

    Task AddRestoreProofAsync(
        ReservationAnonymisationRestoreReceipt receipt,
        ReservationAnonymisationTombstone? newTombstone,
        CancellationToken cancellationToken);
}
