namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class RecordControllerRoutingCommandHandler(
    DataRightsCaseMutationCoordinator mutations,
    IDataRightsResponseDeadlinePolicy responseDeadlinePolicy,
    ISystemClock clock) : ICommandHandler<RecordControllerRoutingCommand, DataRightsCaseDto>
{
    public async Task<Result<DataRightsCaseDto>> HandleAsync(
        RecordControllerRoutingCommand command,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await mutations.AcquireAsync(
            command.Scope,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsCaseDto>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        DataRightsResponseDeadlinePolicyEvidence? deadlineEvidence = null;
        if (RequiresGuestResponseDeadline(dataRightsCase) &&
            dataRightsCase.ResponseDeadlinePolicyEvidence is null)
        {
            Result<DataRightsResponseDeadlinePolicyEvidence> deadline =
                await responseDeadlinePolicy.ResolveGuestAsync(
                    dataRightsCase.PropertyId!.Value,
                    dataRightsCase.RequestedOperations,
                    dataRightsCase.CreatedAtUtc,
                    nowUtc,
                    cancellationToken).ConfigureAwait(false);
            if (deadline.IsFailure)
            {
                return Result.Failure<DataRightsCaseDto>(deadline.Error);
            }

            deadlineEvidence = deadline.Value;
        }

        Result result = dataRightsCase.RecordControllerRouting(
            command.ExpectedVersion,
            command.ActorId,
            nowUtc,
            deadlineEvidence);
        return result.IsSuccess
            ? Result.Success(dataRightsCase.ToDto())
            : Result.Failure<DataRightsCaseDto>(result.Error);
    }

    private static bool RequiresGuestResponseDeadline(DataRightsCase dataRightsCase) =>
        dataRightsCase.Kind == DataRightsCaseKind.GuestRights &&
        dataRightsCase.RequesterRelationship is
            DataRightsRequesterRelation.DataSubject or
            DataRightsRequesterRelation.AuthorizedRepresentative;
}
