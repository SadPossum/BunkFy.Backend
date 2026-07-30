namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class RequireDataRightsReviewCommandHandler(
    IDataRightsCaseRepository cases,
    DataRightsRequiredCompanionExpander companionExpander,
    ISystemClock clock) : ICommandHandler<RequireDataRightsReviewCommand, DataRightsCaseDto>
{
    public async Task<Result<DataRightsCaseDto>> HandleAsync(
        RequireDataRightsReviewCommand command,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            command.Scope,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsCaseDto>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        Result<IReadOnlyCollection<DataRightsSubjectSelection>> companions =
            await companionExpander.ExpandAsync(
                dataRightsCase,
                cancellationToken).ConfigureAwait(false);
        if (companions.IsFailure)
        {
            return Result.Failure<DataRightsCaseDto>(companions.Error);
        }

        Result transitioned = dataRightsCase.RequireReview(
            companions.Value,
            command.ExpectedVersion,
            command.ActorId,
            clock.UtcNow);
        return transitioned.IsSuccess
            ? Result.Success(dataRightsCase.ToDto())
            : Result.Failure<DataRightsCaseDto>(transitioned.Error);
    }
}
