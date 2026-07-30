namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Retention;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffRetentionExecutionRepository(
    StaffDbContext dbContext)
    : IStaffRetentionExecutionRepository
{
    public Task<StaffRetentionExecution?> GetExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken) =>
        dbContext.RetentionExecutions.SingleOrDefaultAsync(
            execution => execution.Id == executionId,
            cancellationToken);

    public Task AddExecutionAsync(
        StaffRetentionExecution execution,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionExecutions.Add(execution);
        return Task.CompletedTask;
    }

    public Task<StaffRetentionSweepCheckpoint?> GetCheckpointAsync(
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
        StaffRetentionSweepCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionSweepCheckpoints.Add(checkpoint);
        return Task.CompletedTask;
    }

    public Task<StaffRetentionAnonymisationReceipt?> GetReceiptAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        dbContext.RetentionAnonymisationReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt =>
                    receipt.StaffMemberId == staffMemberId,
                cancellationToken);

    public Task<StaffAnonymisationTombstone?> GetTombstoneAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationTombstones
            .AsNoTracking()
            .SingleOrDefaultAsync(
                tombstone => tombstone.Id == staffMemberId,
                cancellationToken);

    public Task AddAnonymisationProofAsync(
        StaffRetentionAnonymisationReceipt receipt,
        StaffAnonymisationTombstone tombstone,
        CancellationToken cancellationToken)
    {
        dbContext.RetentionAnonymisationReceipts.Add(receipt);
        dbContext.AnonymisationTombstones.Add(tombstone);
        return Task.CompletedTask;
    }
}
