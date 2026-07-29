namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(
    DataRightsModuleMetadata.AnonymisationWorkItemTerminalHandlerName)]
internal sealed class DataRightsAnonymisationWorkItemTerminalHandler(
    DataRightsAnonymisationExecutionReconciler reconciler)
    : IIntegrationEventHandler<
        DataRightsAnonymisationWorkItemTerminalIntegrationEvent>
{
    public Task HandleAsync(
        DataRightsAnonymisationWorkItemTerminalIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        reconciler.ReconcileAsync(
            DataRightsCaseScope.ForProperty(integrationEvent.PropertyId),
            integrationEvent.BatchId,
            integrationEvent.WorkItemId,
            integrationEvent.CaseId,
            integrationEvent.ExecutionRevision,
            cancellationToken);
}
