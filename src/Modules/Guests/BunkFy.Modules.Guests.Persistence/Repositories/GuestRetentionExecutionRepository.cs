namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Retention;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestRetentionExecutionRepository(
    GuestsDbContext dbContext)
    : IGuestRetentionExecutionRepository
{
    public Task<GuestRetentionExecution?> GetExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken) =>
        dbContext.RetentionExecutions.SingleOrDefaultAsync(
            execution => execution.Id == executionId,
            cancellationToken);

    public Task AddExecutionAsync(
        GuestRetentionExecution execution,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionExecutions.Add(execution);
        return Task.CompletedTask;
    }

    public Task<GuestRetentionSweepCheckpoint?> GetCheckpointAsync(
        string dataClassKey,
        CancellationToken cancellationToken) =>
        dbContext.RetentionSweepCheckpoints.SingleOrDefaultAsync(
            checkpoint => checkpoint.DataClassKey == dataClassKey,
            cancellationToken);

    public Task AddCheckpointAsync(
        GuestRetentionSweepCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionSweepCheckpoints.Add(checkpoint);
        return Task.CompletedTask;
    }

    public Task<GuestRetentionAnonymisationReceipt?> GetReceiptAsync(
        Guid guestId,
        CancellationToken cancellationToken) =>
        dbContext.RetentionAnonymisationReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.GuestId == guestId,
                cancellationToken);

    public Task<GuestAnonymisationTombstone?> GetTombstoneAsync(
        Guid guestId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationTombstones
            .AsNoTracking()
            .SingleOrDefaultAsync(
                tombstone => tombstone.Id == guestId,
                cancellationToken);

    public Task<GuestProfile?> GetProfileAsync(
        Guid guestId,
        CancellationToken cancellationToken) =>
        dbContext.GuestProfiles.FirstOrDefaultAsync(
            profile =>
                profile.Id == guestId &&
                (profile.Status == GuestProfileState.Active ||
                 profile.Status == GuestProfileState.Archived),
            cancellationToken);

    public Task AddAnonymisationProofAsync(
        GuestRetentionAnonymisationReceipt receipt,
        GuestAnonymisationTombstone tombstone,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionAnonymisationReceipts.Add(receipt);
        dbContext.AnonymisationTombstones.Add(tombstone);
        return Task.CompletedTask;
    }
}
