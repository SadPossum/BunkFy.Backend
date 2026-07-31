namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.Termination;
using Microsoft.EntityFrameworkCore;
using ContractFenceState =
    BunkFy.Modules.Workspaces.Contracts.WorkspaceTerminationFenceState;
using DomainFenceState =
    BunkFy.Modules.Workspaces.Domain.Termination.WorkspaceTerminationFenceState;

internal sealed class WorkspaceTerminationFenceRepository(
    WorkspacesDbContext dbContext)
    : IWorkspaceTerminationFenceRepository,
      IWorkspaceTerminationFenceReader
{
    public async Task<WorkspaceTerminationFence?> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.WorkspaceTerminationFences
            .SingleOrDefaultAsync(
                fence => fence.State != DomainFenceState.Released,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<WorkspaceTerminationFence?> GetByProcessAsync(
        Guid processId,
        CancellationToken cancellationToken = default) =>
        await dbContext.WorkspaceTerminationFences
            .SingleOrDefaultAsync(
                fence => fence.ProcessId == processId,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> HasCoordinatesAsync(
        Guid processId,
        Guid terminationEpoch,
        CancellationToken cancellationToken = default) =>
        await dbContext.WorkspaceTerminationFences
            .AnyAsync(
                fence =>
                    fence.ProcessId == processId ||
                    fence.TerminationEpoch == terminationEpoch,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<WorkspaceTerminationFenceReceipt?> FindReceiptAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) =>
        await dbContext.WorkspaceTerminationFenceReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.IdempotencyKey == idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> TryLockAsync(
        Guid fenceId,
        CancellationToken cancellationToken = default)
    {
        if (fenceId == Guid.Empty)
        {
            throw new ArgumentException(
                "A workspace termination lock requires a fence identifier.",
                nameof(fenceId));
        }

        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A workspace termination lock requires an active transaction.");
        }

        if (!dbContext.Database.IsRelational())
        {
            return await dbContext.WorkspaceTerminationFences
                .AsNoTracking()
                .AnyAsync(
                    fence => fence.Id == fenceId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        int affected = await dbContext.WorkspaceTerminationFences
            .Where(fence => fence.Id == fenceId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    fence => fence.Version,
                    fence => fence.Version),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    public async Task AddAsync(
        WorkspaceTerminationFence fence,
        CancellationToken cancellationToken = default) =>
        await dbContext.WorkspaceTerminationFences
            .AddAsync(fence, cancellationToken).ConfigureAwait(false);

    public async Task AddReceiptAsync(
        WorkspaceTerminationFenceReceipt receipt,
        CancellationToken cancellationToken = default) =>
        await dbContext.WorkspaceTerminationFenceReceipts
            .AddAsync(receipt, cancellationToken).ConfigureAwait(false);

    async Task<WorkspaceTerminationFenceSnapshot?>
        IWorkspaceTerminationFenceReader.GetCurrentAsync(
            CancellationToken cancellationToken)
    {
        WorkspaceTerminationFence? fence =
            await dbContext.WorkspaceTerminationFences
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.State != DomainFenceState.Released,
                    cancellationToken)
                .ConfigureAwait(false);
        if (fence is null)
        {
            return null;
        }

        ContractFenceState state = fence.State switch
        {
            DomainFenceState.Frozen => ContractFenceState.Frozen,
            DomainFenceState.DestructionStarted =>
                ContractFenceState.DestructionStarted,
            DomainFenceState.Closed => ContractFenceState.Closed,
            _ => ContractFenceState.Unknown
        };
        if (state == ContractFenceState.Unknown)
        {
            throw new InvalidOperationException(
                "The active workspace termination fence state is invalid.");
        }

        return new WorkspaceTerminationFenceSnapshot(
            fence.ProcessId,
            fence.TerminationEpoch,
            state,
            fence.Version);
    }
}
