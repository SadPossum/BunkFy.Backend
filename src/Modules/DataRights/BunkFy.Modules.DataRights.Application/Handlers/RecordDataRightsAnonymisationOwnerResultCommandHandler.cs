namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class RecordDataRightsAnonymisationOwnerResultCommandHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExecutionWorkItemRepository workItems,
    ISystemClock clock)
    : ICommandHandler<RecordDataRightsAnonymisationOwnerResultCommand, Unit>
{
    private const string SystemActor = "system:data-rights-anonymisation";

    public async Task<Result<Unit>> HandleAsync(
        RecordDataRightsAnonymisationOwnerResultCommand command,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            command.PropertyId,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        DataRightsExecutionWorkItem? workItem = await workItems.GetAsync(
            command.PropertyId,
            command.CaseId,
            command.WorkItemId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null || workItem is null)
        {
            return Result.Failure<Unit>(DataRightsApplicationErrors.ExecutionNotFound);
        }

        if (!workItem.HasExecutionCoordinates(
                command.CaseId,
                command.PropertyId,
                command.ApprovalRevision,
                command.ExecutionRevision) ||
            workItem.OwnerContractVersion != command.Result.ContractVersion)
        {
            return Result.Failure<Unit>(
                DataRightsApplicationErrors.ExecutionCoordinateInvalid);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result recorded = command.Result.Status switch
        {
            DataRightsAnonymisationContributionStatus.Completed
                when command.Result.OwnerProof is { } proof &&
                     command.Result.OutcomeCode is null =>
                workItem.RecordOwnerProof(
                    command.ExpectedWorkItemVersion,
                    command.TaskRunId,
                    command.TaskAttempt,
                    proof.ReceiptContractVersion,
                    proof.ReceiptId,
                    proof.ResultingRecordVersion,
                    proof.DispositionCode,
                    proof.ReasonCode,
                    proof.ReceiptSha256,
                    proof.CompletedAtUtc,
                    nowUtc),
            DataRightsAnonymisationContributionStatus.Blocked
                when command.Result.OwnerProof is null =>
                workItem.RecordBlocked(
                    command.ExpectedWorkItemVersion,
                    command.TaskRunId,
                    command.TaskAttempt,
                    command.Result.OutcomeCode ?? string.Empty,
                    nowUtc),
            DataRightsAnonymisationContributionStatus.Failed
                when command.Result.OwnerProof is null =>
                workItem.RecordFailed(
                    command.ExpectedWorkItemVersion,
                    command.TaskRunId,
                    command.TaskAttempt,
                    command.Result.OutcomeCode ?? string.Empty,
                    nowUtc),
            _ => Result.Failure(
                DataRightsApplicationErrors.ExecutionOwnerResultInvalid)
        };
        if (recorded.IsFailure)
        {
            return Result.Failure<Unit>(recorded.Error);
        }

        if (workItem.State is DataRightsExecutionWorkItemState.Blocked
                or DataRightsExecutionWorkItemState.Failed)
        {
            Result caseBlocked = dataRightsCase.BlockAnonymisationExecution(
                dataRightsCase.Version,
                SystemActor,
                nowUtc);
            if (caseBlocked.IsFailure)
            {
                return Result.Failure<Unit>(caseBlocked.Error);
            }
        }

        return Result.Success(Unit.Value);
    }
}
