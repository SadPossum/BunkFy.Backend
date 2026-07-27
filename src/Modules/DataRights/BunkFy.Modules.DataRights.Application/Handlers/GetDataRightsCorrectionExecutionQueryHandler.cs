namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetDataRightsCorrectionExecutionQueryHandler(
    IDataRightsCorrectionExecutionRepository executions)
    : IQueryHandler<
        GetDataRightsCorrectionExecutionQuery,
        DataRightsCorrectionExecutionDetailsDto>
{
    public async Task<Result<DataRightsCorrectionExecutionDetailsDto>> HandleAsync(
        GetDataRightsCorrectionExecutionQuery query,
        CancellationToken cancellationToken)
    {
        DataRightsCorrectionExecution? execution = await executions.GetByCaseAsync(
            query.PropertyId,
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        return execution is null
            ? Result.Failure<DataRightsCorrectionExecutionDetailsDto>(
                DataRightsApplicationErrors.CorrectionExecutionNotFound)
            : Result.Success(execution.ToDto());
    }
}
