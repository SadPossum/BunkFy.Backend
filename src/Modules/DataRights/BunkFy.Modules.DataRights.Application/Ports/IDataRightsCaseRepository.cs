namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Pagination;

public interface IDataRightsCaseRepository
{
    Task AddAsync(DataRightsCase dataRightsCase, CancellationToken cancellationToken);

    Task<DataRightsCase?> GetAsync(
        Guid propertyId,
        Guid caseId,
        CancellationToken cancellationToken);

    Task<DataRightsCaseListResponse> ListAsync(
        Guid propertyId,
        DataRightsCaseStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
