namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Pagination;

public interface IDataRightsCaseRepository
{
    Task AddAsync(DataRightsCase dataRightsCase, CancellationToken cancellationToken);

    Task<DataRightsCase?> GetAsync(
        DataRightsCaseScope scope,
        Guid caseId,
        CancellationToken cancellationToken);

    Task<DataRightsCaseListResponse> ListAsync(
        DataRightsCaseScope scope,
        DataRightsCaseStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
