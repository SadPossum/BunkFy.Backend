namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetDataRightsExecutionQueryHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExecutionBatchRepository batches,
    IDataRightsExecutionWorkItemRepository workItems)
    : IQueryHandler<GetDataRightsExecutionQuery, DataRightsExecutionDto>
{
    public async Task<Result<DataRightsExecutionDto>> HandleAsync(
        GetDataRightsExecutionQuery query,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            query.Scope,
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        DataRightsExecutionBatch? batch = await batches.GetByCaseAsync(
            query.Scope,
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.ExecutionNotFound);
        }

        IReadOnlyCollection<DataRightsExecutionWorkItem> executionItems =
            await workItems.ListByBatchAsync(
                query.Scope,
                query.CaseId,
                batch.Id,
                cancellationToken).ConfigureAwait(false);
        return executionItems.Count != batch.SelectedSubjectCount
            ? Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.ExecutionStateInvalid)
            : Result.Success(new DataRightsExecutionDto(
                dataRightsCase.ToDto(),
                batch.ToDto(),
                executionItems.Select(workItem => workItem.ToDto()).ToArray()));
    }
}
