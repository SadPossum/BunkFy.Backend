namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Domain.Termination;

public interface IWorkspaceTerminationFenceRepository
{
    Task<WorkspaceTerminationFence?> GetActiveAsync(
        CancellationToken cancellationToken = default);

    Task<WorkspaceTerminationFence?> GetByProcessAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    Task<bool> HasCoordinatesAsync(
        Guid processId,
        Guid terminationEpoch,
        CancellationToken cancellationToken = default);

    Task<WorkspaceTerminationFenceReceipt?> FindReceiptAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<bool> TryLockAsync(
        Guid fenceId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        WorkspaceTerminationFence fence,
        CancellationToken cancellationToken = default);

    Task AddReceiptAsync(
        WorkspaceTerminationFenceReceipt receipt,
        CancellationToken cancellationToken = default);
}
