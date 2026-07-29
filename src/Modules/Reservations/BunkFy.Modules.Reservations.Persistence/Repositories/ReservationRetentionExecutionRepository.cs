namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Retention;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationRetentionExecutionRepository(
    ReservationsDbContext dbContext)
    : IReservationRetentionExecutionRepository
{
    public Task<ReservationRetentionExecution?> GetExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken) =>
        dbContext.RetentionExecutions.SingleOrDefaultAsync(
            execution => execution.Id == executionId,
            cancellationToken);

    public Task AddExecutionAsync(
        ReservationRetentionExecution execution,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionExecutions.Add(execution);
        return Task.CompletedTask;
    }

    public Task<ReservationRetentionSweepCheckpoint?> GetCheckpointAsync(
        string dataClassKey,
        int executionPolicyVersion,
        CancellationToken cancellationToken) =>
        dbContext.RetentionSweepCheckpoints.SingleOrDefaultAsync(
            checkpoint =>
                checkpoint.DataClassKey == dataClassKey &&
                checkpoint.ExecutionPolicyVersion ==
                    executionPolicyVersion,
            cancellationToken);

    public Task AddCheckpointAsync(
        ReservationRetentionSweepCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionSweepCheckpoints.Add(checkpoint);
        return Task.CompletedTask;
    }

    public Task<ReservationRetentionAnonymisationReceipt?>
        GetReceiptAsync(
            Guid reservationId,
            CancellationToken cancellationToken) =>
        dbContext.RetentionAnonymisationReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.ReservationId == reservationId,
                cancellationToken);

    public Task<ReservationAnonymisationTombstone?> GetTombstoneAsync(
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationTombstones
            .AsNoTracking()
            .SingleOrDefaultAsync(
                tombstone => tombstone.Id == reservationId,
                cancellationToken);

    public Task AddAnonymisationProofAsync(
        ReservationRetentionAnonymisationReceipt receipt,
        ReservationAnonymisationTombstone tombstone,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionAnonymisationReceipts.Add(receipt);
        dbContext.AnonymisationTombstones.Add(tombstone);
        return Task.CompletedTask;
    }
}
