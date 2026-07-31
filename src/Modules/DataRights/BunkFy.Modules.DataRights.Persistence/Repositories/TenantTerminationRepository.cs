namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class TenantTerminationRepository(
    DataRightsDbContext dbContext) : ITenantTerminationRepository
{
    public Task AddProcessAsync(
        TenantTerminationProcess process,
        CancellationToken cancellationToken)
    {
        dbContext.TenantTerminationProcesses.Add(process);
        return Task.CompletedTask;
    }

    public Task AddOwnerWorkItemAsync(
        TenantTerminationOwnerWorkItem workItem,
        CancellationToken cancellationToken)
    {
        dbContext.TenantTerminationOwnerWorkItems.Add(workItem);
        return Task.CompletedTask;
    }

    public Task<TenantTerminationProcess?> GetProcessAsync(
        Guid processId,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationProcesses.SingleOrDefaultAsync(
            process => process.Id == processId,
            cancellationToken);

    public Task<TenantTerminationProcess?> GetActiveProcessAsync(
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationProcesses.SingleOrDefaultAsync(
            process =>
                process.Status != TenantTerminationProcessStatus.Completed &&
                process.Status != TenantTerminationProcessStatus.Cancelled,
            cancellationToken);

    public Task<TenantTerminationProcess?> GetProcessByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.TenantTerminationProcesses.SingleOrDefaultAsync(
            process => process.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<TenantTerminationOwnerWorkItem?> GetOwnerWorkItemAsync(
        Guid processId,
        TenantTerminationOwnerPhase phase,
        string ownerKey,
        long operationRevision,
        CancellationToken cancellationToken)
    {
        string normalizedOwnerKey = ownerKey.Trim();
        return dbContext.TenantTerminationOwnerWorkItems.SingleOrDefaultAsync(
            workItem =>
                workItem.ProcessId == processId &&
                workItem.Phase == phase &&
                workItem.OwnerKey == normalizedOwnerKey &&
                workItem.OperationRevision == operationRevision,
            cancellationToken);
    }

    public async Task<IReadOnlyList<TenantTerminationOwnerWorkItem>>
        ListOwnerWorkItemsAsync(
            Guid processId,
            TenantTerminationOwnerPhase phase,
            long operationRevision,
            CancellationToken cancellationToken) =>
        await dbContext.TenantTerminationOwnerWorkItems
            .Where(workItem =>
                workItem.ProcessId == processId &&
                workItem.Phase == phase &&
                workItem.OperationRevision == operationRevision)
            .OrderBy(workItem => workItem.OwnerKey)
            .ThenBy(workItem => workItem.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
