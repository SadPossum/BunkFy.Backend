namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;

public interface ITenantTerminationRepository
{
    Task AddProcessAsync(
        TenantTerminationProcess process,
        CancellationToken cancellationToken);

    Task AddOwnerWorkItemAsync(
        TenantTerminationOwnerWorkItem workItem,
        CancellationToken cancellationToken);

    Task<TenantTerminationProcess?> GetProcessAsync(
        Guid processId,
        CancellationToken cancellationToken);

    Task<TenantTerminationProcess?> GetActiveProcessAsync(
        CancellationToken cancellationToken);

    Task<TenantTerminationProcess?> GetProcessByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<TenantTerminationOwnerWorkItem?> GetOwnerWorkItemAsync(
        Guid processId,
        TenantTerminationOwnerPhase phase,
        string ownerKey,
        long operationRevision,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantTerminationOwnerWorkItem>> ListOwnerWorkItemsAsync(
        Guid processId,
        TenantTerminationOwnerPhase phase,
        long operationRevision,
        CancellationToken cancellationToken);
}
