namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface ITenantTerminationOperatorStatusRepository
{
    Task<TenantTerminationProcess?> GetProcessByCaseIdAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantTerminationOwnerWorkItem>>
        ListOwnerWorkItemsAsync(
            Guid processId,
            CancellationToken cancellationToken);
}
