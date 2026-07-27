namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Results;

internal static class DataRightsCaseCommandExecution
{
    public static async Task<Result<DataRightsCaseDto>> ApplyAsync(
        IDataRightsCaseRepository cases,
        DataRightsCaseScope scope,
        Guid caseId,
        Func<DataRightsCase, Result> mutation,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            scope,
            caseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsCaseDto>(DataRightsApplicationErrors.CaseNotFound);
        }

        Result result = mutation(dataRightsCase);
        return result.IsSuccess
            ? Result.Success(dataRightsCase.ToDto())
            : Result.Failure<DataRightsCaseDto>(result.Error);
    }
}
