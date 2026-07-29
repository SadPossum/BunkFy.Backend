namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface IDataRightsExecutionWorkItemRepository
{
    Task AddAsync(
        DataRightsExecutionWorkItem workItem,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DataRightsExecutionWorkItem>> ListByBatchAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        Guid batchId,
        CancellationToken cancellationToken);

    Task<DataRightsExecutionWorkItem?> GetAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        Guid workItemId,
        CancellationToken cancellationToken);
}
