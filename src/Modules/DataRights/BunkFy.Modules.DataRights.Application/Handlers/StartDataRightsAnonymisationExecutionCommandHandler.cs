namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using SelectedSubject = BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate;

internal sealed class StartDataRightsAnonymisationExecutionCommandHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExecutionWorkItemRepository workItems,
    IDataRightsOperationApprovalGate approvalGate,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<StartDataRightsAnonymisationExecutionCommand, DataRightsExecutionDto>
{
    public async Task<Result<DataRightsExecutionDto>> HandleAsync(
        StartDataRightsAnonymisationExecutionCommand command,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            command.PropertyId,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        DataRightsExecutionWorkItem? existing = await workItems.GetByCaseAsync(
            command.PropertyId,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.HasIdempotencyKey(command.IdempotencyKey) &&
                dataRightsCase.ExecutionRevision == existing.ExecutionRevision
                ? Result.Success(ToExecution(dataRightsCase, existing))
                : Result.Failure<DataRightsExecutionDto>(
                    DataRightsApplicationErrors.ExecutionAlreadyStarted);
        }

        if (dataRightsCase.SelectedSubjects.Count != 1)
        {
            return Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.AnonymisationSubjectCountInvalid);
        }

        SelectedSubject subject = dataRightsCase.SelectedSubjects.Single();
        DataRightsApprovalPolicyEvidence? evidence = dataRightsCase.ApprovalPolicyEvidence;
        if (evidence is null ||
            dataRightsCase.PropertyId != command.PropertyId ||
            dataRightsCase.DecisionRevision is not long approvalRevision)
        {
            return Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.AnonymisationExecutionDenied);
        }

        DataRightsOperationApprovalResult approval = await approvalGate.EvaluateAsync(
            new DataRightsOperationApprovalRequest(
                dataRightsCase.ScopeId,
                command.PropertyId,
                command.CaseId,
                approvalRevision,
                DataRightsOperation.Anonymisation,
                subject.OwnerKey,
                subject.RecordType,
                subject.RecordId,
                subject.RecordVersion,
                ExecutingActorId: command.ActorId),
            cancellationToken).ConfigureAwait(false);
        if (!approval.IsApproved || approval.ApprovalEvidence is null)
        {
            return Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.AnonymisationExecutionDenied);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result transition = dataRightsCase.BeginAnonymisationExecution(
            command.ExpectedVersion,
            command.ActorId,
            nowUtc);
        if (transition.IsFailure)
        {
            return Result.Failure<DataRightsExecutionDto>(transition.Error);
        }

        Result<DataRightsExecutionWorkItem> prepared = DataRightsExecutionWorkItem.Prepare(
            ids.NewId(),
            dataRightsCase.ScopeId,
            command.IdempotencyKey,
            dataRightsCase.Id,
            command.PropertyId,
            approvalRevision,
            dataRightsCase.ExecutionRevision!.Value,
            DataRightsCaseOperation.Anonymisation,
            subject,
            evidence,
            command.ActorId,
            nowUtc);
        if (prepared.IsFailure)
        {
            return Result.Failure<DataRightsExecutionDto>(prepared.Error);
        }

        await workItems.AddAsync(prepared.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(ToExecution(dataRightsCase, prepared.Value));
    }

    private static DataRightsExecutionDto ToExecution(
        DataRightsCase dataRightsCase,
        DataRightsExecutionWorkItem workItem) =>
        new(dataRightsCase.ToDto(), workItem.ToDto());
}
