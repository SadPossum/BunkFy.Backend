namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface IDataRightsCorrectionExecutionRepository
{
    Task AddAsync(
        DataRightsCorrectionExecution execution,
        CancellationToken cancellationToken);

    Task<DataRightsCorrectionExecution?> GetAsync(
        Guid propertyId,
        Guid caseId,
        Guid executionId,
        CancellationToken cancellationToken);

    Task<DataRightsCorrectionExecution?> GetByCaseAsync(
        Guid propertyId,
        Guid caseId,
        CancellationToken cancellationToken);
}
