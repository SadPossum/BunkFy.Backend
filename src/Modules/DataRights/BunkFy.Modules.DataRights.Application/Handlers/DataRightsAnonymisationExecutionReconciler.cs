namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class DataRightsAnonymisationExecutionReconciler(
    DataRightsCaseMutationCoordinator mutations,
    IDataRightsExecutionBatchRepository batches,
    IDataRightsExecutionWorkItemRepository workItems,
    ISystemClock clock)
{
    private const string SystemActor =
        "system:data-rights-anonymisation";

    public async Task ReconcileAsync(
        DataRightsCaseScope scope,
        Guid batchId,
        Guid workItemId,
        Guid caseId,
        long executionRevision,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await mutations.AcquireAsync(
            scope,
            caseId,
            cancellationToken).ConfigureAwait(false);
        DataRightsExecutionBatch? batch = await batches.GetByCaseAsync(
            scope,
            caseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null ||
            batch is null ||
            batch.Id != batchId ||
            batch.ExecutionRevision != executionRevision ||
            dataRightsCase.ExecutionRevision != executionRevision)
        {
            throw new InvalidOperationException(
                "DataRights.ExecutionReconciliationCoordinatesInvalid");
        }

        IReadOnlyCollection<DataRightsExecutionWorkItem> executionItems =
            await workItems.ListByBatchAsync(
                scope,
                caseId,
                batchId,
                cancellationToken).ConfigureAwait(false);
        if (executionItems.Count != batch.SelectedSubjectCount ||
            !executionItems.Any(item => item.Id == workItemId) ||
            executionItems.Any(item =>
                item.BatchId != batch.Id ||
                item.ExecutionRevision != batch.ExecutionRevision))
        {
            throw new InvalidOperationException(
                "DataRights.ExecutionReconciliationStateInvalid");
        }

        if (executionItems.Any(item => item.State is
                DataRightsExecutionWorkItemState.Prepared or
                DataRightsExecutionWorkItemState.Processing or
                DataRightsExecutionWorkItemState.OwnerProofRecorded))
        {
            return;
        }

        int completed = executionItems.Count(item =>
            item.State == DataRightsExecutionWorkItemState.Completed);
        int noOp = executionItems.Count(item =>
            item.State == DataRightsExecutionWorkItemState.NoOp);
        int blocked = executionItems.Count(item =>
            item.State == DataRightsExecutionWorkItemState.Blocked);
        int failed = executionItems.Count(item =>
            item.State == DataRightsExecutionWorkItemState.Failed);
        if (completed + noOp + blocked + failed != executionItems.Count)
        {
            throw new InvalidOperationException(
                "DataRights.ExecutionReconciliationOutcomeInvalid");
        }

        Result reconciled = dataRightsCase.ReconcileAnonymisationExecution(
            dataRightsCase.Version,
            executionItems.Count,
            completed,
            noOp,
            blocked,
            failed,
            SystemActor,
            clock.UtcNow);
        if (reconciled.IsFailure)
        {
            throw new InvalidOperationException(
                $"{reconciled.Error.Code}: {reconciled.Error.Message}");
        }
    }
}
