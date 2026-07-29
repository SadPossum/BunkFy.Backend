namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RecordDataRightsAnonymisationOwnerResultCommandHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExecutionWorkItemRepository workItems,
    IOutboxWriterRegistry outboxWriters,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<RecordDataRightsAnonymisationOwnerResultCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        RecordDataRightsAnonymisationOwnerResultCommand command,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            command.Scope,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        DataRightsExecutionWorkItem? workItem = await workItems.GetAsync(
            command.Scope,
            command.CaseId,
            command.WorkItemId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null || workItem is null)
        {
            return Result.Failure<Unit>(DataRightsApplicationErrors.ExecutionNotFound);
        }

        DataRightsExecutionScope executionScope =
            command.Scope.ToExecutionScope();
        if (!workItem.HasExecutionCoordinates(
                command.CaseId,
                executionScope,
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
            if (workItem.PropertyId is not Guid propertyId)
            {
                return Result.Failure<Unit>(
                    DataRightsApplicationErrors.ExecutionCoordinateInvalid);
            }

            await outboxWriters.GetRequired(DataRightsModuleMetadata.Name).EnqueueAsync(
                new DataRightsAnonymisationWorkItemTerminalIntegrationEvent(
                    ids.NewId(),
                    workItem.ScopeId,
                    nowUtc,
                    workItem.BatchId,
                    workItem.Id,
                    workItem.CaseId,
                    propertyId,
                    workItem.ExecutionRevision),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(Unit.Value);
    }
}
