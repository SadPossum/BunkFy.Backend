namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface ITenantTerminationExportFragmentRepository
{
    Task AddAsync(
        TenantTerminationExportFragment fragment,
        CancellationToken cancellationToken);

    Task<TenantTerminationExportFragment?> GetAsync(
        Guid workItemId,
        CancellationToken cancellationToken);

    Task<TenantTerminationExportFragment?> GetByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantTerminationExportFragment>> ListAsync(
        Guid processId,
        long exportOperationRevision,
        CancellationToken cancellationToken);
}
