namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetDataRightsExecutionQueryHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExecutionWorkItemRepository workItems)
    : IQueryHandler<GetDataRightsExecutionQuery, DataRightsExecutionDto>
{
    public async Task<Result<DataRightsExecutionDto>> HandleAsync(
        GetDataRightsExecutionQuery query,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            query.PropertyId,
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        DataRightsExecutionWorkItem? workItem = await workItems.GetByCaseAsync(
            query.PropertyId,
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        return workItem is null
            ? Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.ExecutionNotFound)
            : Result.Success(new DataRightsExecutionDto(
                dataRightsCase.ToDto(),
                workItem.ToDto()));
    }
}
