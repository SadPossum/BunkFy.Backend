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
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using SelectedSubject = BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate;

internal sealed class StartDataRightsAnonymisationExecutionCommandHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExecutionBatchRepository batches,
    IDataRightsExecutionWorkItemRepository workItems,
    IDataRightsOperationApprovalGate approvalGate,
    IOutboxWriterRegistry outboxWriters,
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

        DataRightsExecutionBatch? existing = await batches.GetByCaseAsync(
            command.PropertyId,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            IReadOnlyCollection<DataRightsExecutionWorkItem> existingItems =
                await workItems.ListByBatchAsync(
                    command.PropertyId,
                    command.CaseId,
                    existing.Id,
                    cancellationToken).ConfigureAwait(false);
            return existing.Matches(
                    command.IdempotencyKey,
                    command.CaseId,
                    command.PropertyId,
                    dataRightsCase.ExecutionRevision) &&
                MatchesBatch(existing, existingItems)
                ? Result.Success(ToExecution(dataRightsCase, existing, existingItems))
                : Result.Failure<DataRightsExecutionDto>(
                    DataRightsApplicationErrors.ExecutionAlreadyStarted);
        }

        if (dataRightsCase.SelectedSubjects.Count is
            <= 0 or > DataRightsCase.MaxSelectedSubjects)
        {
            return Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.AnonymisationSubjectCountInvalid);
        }

        SelectedSubject[] subjects = dataRightsCase.SelectedSubjects
            .OrderBy(subject => subject.OwnerKey, StringComparer.Ordinal)
            .ThenBy(subject => subject.RecordType, StringComparer.Ordinal)
            .ThenBy(subject => subject.RecordId)
            .ToArray();
        DataRightsApprovalPolicyEvidence? evidence = dataRightsCase.ApprovalPolicyEvidence;
        if (evidence is null ||
            dataRightsCase.PropertyId != command.PropertyId ||
            dataRightsCase.DecisionRevision is not long approvalRevision)
        {
            return Result.Failure<DataRightsExecutionDto>(
                DataRightsApplicationErrors.AnonymisationExecutionDenied);
        }

        foreach (SelectedSubject subject in subjects)
        {
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
            if (!approval.IsApproved ||
                approval.ApprovalEvidence is null ||
                !DataRightsApprovalEvidenceComparer.Matches(
                    evidence,
                    approval.ApprovalEvidence))
            {
                return Result.Failure<DataRightsExecutionDto>(
                    DataRightsApplicationErrors.AnonymisationExecutionDenied);
            }
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

        Guid batchId = ids.NewId();
        Result<DataRightsExecutionBatch> preparedBatch = DataRightsExecutionBatch.Prepare(
            batchId,
            dataRightsCase.ScopeId,
            command.IdempotencyKey,
            dataRightsCase.Id,
            command.PropertyId,
            approvalRevision,
            dataRightsCase.ExecutionRevision!.Value,
            subjects.Length,
            command.ActorId,
            nowUtc);
        if (preparedBatch.IsFailure)
        {
            return Result.Failure<DataRightsExecutionDto>(preparedBatch.Error);
        }

        List<DataRightsExecutionWorkItem> preparedItems = new(subjects.Length);
        foreach (SelectedSubject subject in subjects)
        {
            Result<DataRightsExecutionWorkItem> prepared =
                DataRightsExecutionWorkItem.Prepare(
                    ids.NewId(),
                    dataRightsCase.ScopeId,
                    batchId,
                    DataRightsExecutionIdentity.CreateWorkItemIdempotencyKey(
                        command.IdempotencyKey,
                        subject),
                    dataRightsCase.Id,
                    command.PropertyId,
                    approvalRevision,
                    dataRightsCase.ExecutionRevision.Value,
                    DataRightsCaseOperation.Anonymisation,
                    subject,
                    evidence,
                    command.ActorId,
                    nowUtc);
            if (prepared.IsFailure)
            {
                return Result.Failure<DataRightsExecutionDto>(prepared.Error);
            }

            preparedItems.Add(prepared.Value);
        }

        await batches.AddAsync(preparedBatch.Value, cancellationToken).ConfigureAwait(false);
        IOutboxWriter outbox = outboxWriters.GetRequired(DataRightsModuleMetadata.Name);
        foreach (DataRightsExecutionWorkItem prepared in preparedItems)
        {
            await workItems.AddAsync(prepared, cancellationToken).ConfigureAwait(false);
            await outbox.EnqueueAsync(
                new DataRightsAnonymisationExecutionPreparedIntegrationEvent(
                    ids.NewId(),
                    dataRightsCase.ScopeId,
                    nowUtc,
                    prepared.Id,
                    dataRightsCase.Id,
                    command.PropertyId,
                    approvalRevision,
                    prepared.ExecutionRevision),
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(ToExecution(
            dataRightsCase,
            preparedBatch.Value,
            preparedItems));
    }

    private static bool MatchesBatch(
        DataRightsExecutionBatch batch,
        IReadOnlyCollection<DataRightsExecutionWorkItem> workItems) =>
        workItems.Count == batch.SelectedSubjectCount &&
        workItems.All(workItem =>
            workItem.BatchId == batch.Id &&
            workItem.CaseId == batch.CaseId &&
            workItem.PropertyId == batch.PropertyId &&
            workItem.ApprovalRevision == batch.ApprovalRevision &&
            workItem.ExecutionRevision == batch.ExecutionRevision &&
            workItem.Operation == DataRightsCaseOperation.Anonymisation);

    private static DataRightsExecutionDto ToExecution(
        DataRightsCase dataRightsCase,
        DataRightsExecutionBatch batch,
        IEnumerable<DataRightsExecutionWorkItem> workItems) =>
        new(
            dataRightsCase.ToDto(),
            batch.ToDto(),
            workItems.Select(workItem => workItem.ToDto()).ToArray());
}
