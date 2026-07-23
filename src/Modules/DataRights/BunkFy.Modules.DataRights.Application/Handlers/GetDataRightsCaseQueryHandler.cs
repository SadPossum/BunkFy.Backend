namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetDataRightsCaseQueryHandler(IDataRightsCaseRepository cases)
    : IQueryHandler<GetDataRightsCaseQuery, DataRightsCaseDto>
{
    public async Task<Result<DataRightsCaseDto>> HandleAsync(
        GetDataRightsCaseQuery query,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            query.PropertyId,
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        return dataRightsCase is null
            ? Result.Failure<DataRightsCaseDto>(DataRightsApplicationErrors.CaseNotFound)
            : Result.Success(dataRightsCase.ToDto());
    }
}
