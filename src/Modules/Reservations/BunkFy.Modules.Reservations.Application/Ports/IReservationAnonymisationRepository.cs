namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;

public interface IReservationAnonymisationRepository
{
    Task<ReservationAnonymisationReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<ReservationAnonymisationAffectedRecords> RedactOwnedRecordsAsync(
        Reservation reservation,
        ReservationAnonymisationOutcome outcome,
        CancellationToken cancellationToken);

    Task<bool> VerifyOwnerStateAsync(
        ReservationAnonymisationReceipt receipt,
        CancellationToken cancellationToken);

    Task AddOwnerProofAsync(
        ReservationAnonymisationReceipt receipt,
        ReservationAnonymisationTombstone tombstone,
        CancellationToken cancellationToken);
}

public sealed record ReservationAnonymisationAffectedRecords(
    int RedactedHistoryCount,
    int ReducedExternalOperationCount,
    int SuppressedReminderCount);
