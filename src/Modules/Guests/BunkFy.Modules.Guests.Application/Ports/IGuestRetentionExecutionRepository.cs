namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Retention;

internal interface IGuestRetentionExecutionRepository
{
    Task<GuestRetentionExecution?> GetExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken);

    Task AddExecutionAsync(
        GuestRetentionExecution execution,
        CancellationToken cancellationToken);

    Task<GuestRetentionSweepCheckpoint?> GetCheckpointAsync(
        string dataClassKey,
        CancellationToken cancellationToken);

    Task AddCheckpointAsync(
        GuestRetentionSweepCheckpoint checkpoint,
        CancellationToken cancellationToken);

    Task<GuestRetentionAnonymisationReceipt?> GetReceiptAsync(
        Guid guestId,
        CancellationToken cancellationToken);

    Task<GuestAnonymisationTombstone?> GetTombstoneAsync(
        Guid guestId,
        CancellationToken cancellationToken);

    Task<GuestProfile?> GetProfileAsync(
        Guid guestId,
        CancellationToken cancellationToken);

    Task AddAnonymisationProofAsync(
        GuestRetentionAnonymisationReceipt receipt,
        GuestAnonymisationTombstone tombstone,
        CancellationToken cancellationToken);
}
