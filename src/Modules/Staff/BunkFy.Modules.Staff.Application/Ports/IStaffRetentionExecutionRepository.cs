namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Retention;

internal interface IStaffRetentionExecutionRepository
{
    Task<StaffRetentionExecution?> GetExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken);

    Task AddExecutionAsync(
        StaffRetentionExecution execution,
        CancellationToken cancellationToken);

    Task<StaffRetentionSweepCheckpoint?> GetCheckpointAsync(
        string dataClassKey,
        int executionPolicyVersion,
        CancellationToken cancellationToken);

    Task AddCheckpointAsync(
        StaffRetentionSweepCheckpoint checkpoint,
        CancellationToken cancellationToken);

    Task<StaffRetentionAnonymisationReceipt?> GetReceiptAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);

    Task<StaffAnonymisationTombstone?> GetTombstoneAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);

    Task AddAnonymisationProofAsync(
        StaffRetentionAnonymisationReceipt receipt,
        StaffAnonymisationTombstone tombstone,
        CancellationToken cancellationToken);
}
