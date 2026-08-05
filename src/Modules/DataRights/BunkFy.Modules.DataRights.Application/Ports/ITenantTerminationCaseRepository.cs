namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Aggregates;

public interface ITenantTerminationCaseRepository
{
    Task AddAsync(
        DataRightsCase dataRightsCase,
        CancellationToken cancellationToken);

    Task<DataRightsCase?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task<DataRightsCase?> GetActiveAsync(
        CancellationToken cancellationToken);
}
