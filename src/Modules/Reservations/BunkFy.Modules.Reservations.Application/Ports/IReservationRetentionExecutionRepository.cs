namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Retention;

internal interface IReservationRetentionExecutionRepository
{
    Task<ReservationRetentionExecution?> GetExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken);

    Task AddExecutionAsync(
        ReservationRetentionExecution execution,
        CancellationToken cancellationToken);

    Task<ReservationRetentionSweepCheckpoint?> GetCheckpointAsync(
        string dataClassKey,
        int executionPolicyVersion,
        CancellationToken cancellationToken);

    Task AddCheckpointAsync(
        ReservationRetentionSweepCheckpoint checkpoint,
        CancellationToken cancellationToken);

    Task<ReservationRetentionAnonymisationReceipt?> GetReceiptAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<ReservationAnonymisationTombstone?> GetTombstoneAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    Task AddAnonymisationProofAsync(
        ReservationRetentionAnonymisationReceipt receipt,
        ReservationAnonymisationTombstone tombstone,
        CancellationToken cancellationToken);
}
