namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class BeginDataRightsAnonymisationWorkItemCommandHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExecutionWorkItemRepository workItems,
    IDataRightsOperationApprovalGate approvalGate,
    ISystemClock clock)
    : ICommandHandler<
        BeginDataRightsAnonymisationWorkItemCommand,
        DataRightsAnonymisationWorkItemStart>
{
    internal static readonly TimeSpan OwnerCallTimeout = TimeSpan.FromMinutes(2);
    private const string SystemActor = "system:data-rights-anonymisation";
    private const string ApprovalBlockedCode = "DataRights.ApprovalRevalidationDenied";
    private const string ApprovalEvidenceChangedCode =
        "DataRights.ApprovalEvidenceChanged";

    public async Task<Result<DataRightsAnonymisationWorkItemStart>> HandleAsync(
        BeginDataRightsAnonymisationWorkItemCommand command,
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
            return Result.Failure<DataRightsAnonymisationWorkItemStart>(
                DataRightsApplicationErrors.ExecutionNotFound);
        }

        if (!workItem.HasExecutionCoordinates(
                command.CaseId,
                command.PropertyId,
                command.ApprovalRevision,
                command.ExecutionRevision) ||
            workItem.Operation != DataRightsCaseOperation.Anonymisation ||
            dataRightsCase.ExecutionRevision != command.ExecutionRevision)
        {
            return Result.Failure<DataRightsAnonymisationWorkItemStart>(
                DataRightsApplicationErrors.ExecutionCoordinateInvalid);
        }

        if (workItem.IsOwnerDispatchTerminal)
        {
            return Result.Success(
                DataRightsAnonymisationWorkItemStart.Terminal(workItem.Version));
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result processing = workItem.BeginProcessing(
            command.TaskRunId,
            command.TaskAttempt,
            nowUtc);
        if (processing.IsFailure)
        {
            return Result.Failure<DataRightsAnonymisationWorkItemStart>(
                processing.Error);
        }

        DataRightsOperationApprovalResult approval = await approvalGate.EvaluateAsync(
            new DataRightsOperationApprovalRequest(
                dataRightsCase.ScopeId,
                command.PropertyId,
                command.CaseId,
                command.ApprovalRevision,
                DataRightsOperation.Anonymisation,
                workItem.OwnerKey,
                workItem.RecordType,
                workItem.RecordId,
                workItem.SelectedRecordVersion,
                ExecutingActorId: workItem.CreatedBy),
            cancellationToken).ConfigureAwait(false);
        if (!approval.IsApproved || approval.ApprovalEvidence is null)
        {
            return Block(
                dataRightsCase,
                workItem,
                command,
                ApprovalBlockedCode,
                nowUtc);
        }

        DataRightsApprovalEvidence evidence = approval.ApprovalEvidence;
        if (!MatchesFrozenEvidence(workItem, evidence))
        {
            return Block(
                dataRightsCase,
                workItem,
                command,
                ApprovalEvidenceChangedCode,
                nowUtc);
        }

        DataRightsAnonymisationContributionRequest request = new(
            workItem.OwnerContractVersion,
            dataRightsCase.ScopeId,
            workItem.Id,
            workItem.IdempotencyKey,
            command.PropertyId,
            command.CaseId,
            command.ApprovalRevision,
            command.ExecutionRevision,
            new DataRightsSubjectCoordinate(
                workItem.OwnerKey,
                workItem.RecordType,
                workItem.RecordId,
                workItem.SelectedRecordVersion),
            evidence,
            workItem.CreatedBy,
            nowUtc.Add(OwnerCallTimeout));
        return Result.Success(
            DataRightsAnonymisationWorkItemStart.Ready(workItem.Version, request));
    }

    private static Result<DataRightsAnonymisationWorkItemStart> Block(
        DataRightsCase dataRightsCase,
        DataRightsExecutionWorkItem workItem,
        BeginDataRightsAnonymisationWorkItemCommand command,
        string blockerCode,
        DateTimeOffset nowUtc)
    {
        Result blocked = workItem.RecordBlocked(
            workItem.Version,
            command.TaskRunId,
            command.TaskAttempt,
            blockerCode,
            nowUtc);
        if (blocked.IsFailure)
        {
            return Result.Failure<DataRightsAnonymisationWorkItemStart>(blocked.Error);
        }

        Result caseBlocked = dataRightsCase.BlockAnonymisationExecution(
            dataRightsCase.Version,
            SystemActor,
            nowUtc);
        return caseBlocked.IsFailure
            ? Result.Failure<DataRightsAnonymisationWorkItemStart>(caseBlocked.Error)
            : Result.Success(
                DataRightsAnonymisationWorkItemStart.Terminal(workItem.Version));
    }

    private static bool MatchesFrozenEvidence(
        DataRightsExecutionWorkItem workItem,
        DataRightsApprovalEvidence evidence) =>
        evidence.SchemaVersion == workItem.PolicyEvidenceSchemaVersion &&
        evidence.PropertyId == workItem.PropertyId &&
        string.Equals(evidence.PolicyId, workItem.PolicyId, StringComparison.Ordinal) &&
        evidence.PolicyVersion == workItem.PolicyVersion &&
        string.Equals(
            evidence.RetentionPolicyId,
            workItem.RetentionPolicyId,
            StringComparison.Ordinal) &&
        evidence.RetentionPolicyVersion == workItem.RetentionPolicyVersion &&
        string.Equals(
            evidence.ContentSha256,
            workItem.PolicyContentSha256,
            StringComparison.Ordinal);
}
