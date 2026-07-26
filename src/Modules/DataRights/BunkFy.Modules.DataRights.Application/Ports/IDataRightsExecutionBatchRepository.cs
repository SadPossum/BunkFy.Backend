namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface IDataRightsExecutionBatchRepository
{
    Task AddAsync(
        DataRightsExecutionBatch batch,
        CancellationToken cancellationToken);

    Task<DataRightsExecutionBatch?> GetByCaseAsync(
        Guid propertyId,
        Guid caseId,
        CancellationToken cancellationToken);
}
