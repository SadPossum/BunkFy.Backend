namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Messaging;

[IntegrationEventHandler(
    DataRightsModuleMetadata.AnonymisationWorkItemTerminalV2HandlerName)]
internal sealed class DataRightsAnonymisationWorkItemTerminalV2Handler(
    DataRightsAnonymisationExecutionReconciler reconciler)
    : IIntegrationEventHandler<
        DataRightsAnonymisationWorkItemTerminalIntegrationEventV2>
{
    public Task HandleAsync(
        DataRightsAnonymisationWorkItemTerminalIntegrationEventV2 integrationEvent,
        CancellationToken cancellationToken)
    {
        if (!DataRightsCaseScope.TryCreate(
                integrationEvent.CaseType,
                integrationEvent.PropertyId,
                out DataRightsCaseScope? scope) ||
            scope is null ||
            !MatchesScope(integrationEvent))
        {
            throw new InvalidOperationException(
                "DataRights.ExecutionReconciliationCoordinatesInvalid");
        }

        return reconciler.ReconcileAsync(
            scope,
            integrationEvent.BatchId,
            integrationEvent.WorkItemId,
            integrationEvent.CaseId,
            integrationEvent.ExecutionRevision,
            cancellationToken);
    }

    private static bool MatchesScope(
        DataRightsAnonymisationWorkItemTerminalIntegrationEventV2 integrationEvent) =>
        integrationEvent.ScopeKind switch
        {
            DataRightsExecutionScopeKind.Property =>
                integrationEvent.PropertyId.HasValue,
            DataRightsExecutionScopeKind.Tenant =>
                integrationEvent.PropertyId is null,
            _ => false
        };
}
