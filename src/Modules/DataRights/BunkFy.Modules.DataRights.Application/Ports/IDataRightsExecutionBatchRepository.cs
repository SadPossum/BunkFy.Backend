namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface IDataRightsExecutionBatchRepository
{
    Task AddAsync(
        DataRightsExecutionBatch batch,
        CancellationToken cancellationToken);

    Task<DataRightsExecutionBatch?> GetByCaseAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        CancellationToken cancellationToken);
}
