namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface IDataRightsExecutionWorkItemRepository
{
    Task AddAsync(
        DataRightsExecutionWorkItem workItem,
        CancellationToken cancellationToken);

    Task<DataRightsExecutionWorkItem?> GetByCaseAsync(
        Guid propertyId,
        Guid caseId,
        CancellationToken cancellationToken);
}
