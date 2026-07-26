namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

[IntegrationEventHandler(
    DataRightsModuleMetadata.AnonymisationWorkItemTerminalHandlerName)]
internal sealed class DataRightsAnonymisationWorkItemTerminalHandler(
    IDataRightsCaseRepository cases,
    IDataRightsExecutionBatchRepository batches,
    IDataRightsExecutionWorkItemRepository workItems,
    ISystemClock clock)
    : IIntegrationEventHandler<
        DataRightsAnonymisationWorkItemTerminalIntegrationEvent>
{
    private const string SystemActor = "system:data-rights-anonymisation";

    public async Task HandleAsync(
        DataRightsAnonymisationWorkItemTerminalIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            integrationEvent.PropertyId,
            integrationEvent.CaseId,
            cancellationToken).ConfigureAwait(false);
        DataRightsExecutionBatch? batch = await batches.GetByCaseAsync(
            integrationEvent.PropertyId,
            integrationEvent.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null ||
            batch is null ||
            batch.Id != integrationEvent.BatchId ||
            batch.ExecutionRevision != integrationEvent.ExecutionRevision ||
            dataRightsCase.ExecutionRevision != integrationEvent.ExecutionRevision)
        {
            throw new InvalidOperationException(
                "DataRights.ExecutionReconciliationCoordinatesInvalid");
        }

        IReadOnlyCollection<DataRightsExecutionWorkItem> executionItems =
            await workItems.ListByBatchAsync(
                integrationEvent.PropertyId,
                integrationEvent.CaseId,
                integrationEvent.BatchId,
                cancellationToken).ConfigureAwait(false);
        if (executionItems.Count != batch.SelectedSubjectCount ||
            !executionItems.Any(item => item.Id == integrationEvent.WorkItemId) ||
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
