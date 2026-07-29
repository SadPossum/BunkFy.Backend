namespace BunkFy.Modules.DataRights.Application.Tasks;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.Tasks;

[IntegrationEventHandler(
    DataRightsModuleMetadata.AnonymisationExecutionPreparedV2HandlerName)]
internal sealed class DataRightsAnonymisationExecutionPreparedV2Handler(
    ITaskRunStore taskRuns)
    : IIntegrationEventHandler<
        DataRightsAnonymisationExecutionPreparedIntegrationEventV2>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(
        DataRightsAnonymisationExecutionPreparedIntegrationEventV2 integrationEvent,
        CancellationToken cancellationToken)
    {
        ExecuteDataRightsAnonymisationPayloadV2 payload = new(
            integrationEvent.WorkItemId,
            integrationEvent.CaseId,
            integrationEvent.CaseType,
            integrationEvent.ScopeKind,
            integrationEvent.PropertyId,
            integrationEvent.ApprovalRevision,
            integrationEvent.ExecutionRevision);
        TaskRunRequest request = new(
            integrationEvent.EventId,
            DataRightsModuleMetadata.Name,
            ExecuteDataRightsAnonymisationPayloadV2.TaskName,
            JsonSerializer.Serialize(payload, SerializerOptions),
            integrationEvent.OccurredAtUtc,
            integrationEvent.OccurredAtUtc,
            DataRightsModuleMetadata.AnonymisationWorkerGroup,
            integrationEvent.TenantId,
            integrationEvent.CaseId,
            requestedBy: "system:data-rights",
            maxAttempts: 5,
            payloadVersion:
                ExecuteDataRightsAnonymisationPayloadV2.PayloadVersion,
            deduplicationKey:
                $"{integrationEvent.WorkItemId:N}:" +
                $"{integrationEvent.ExecutionRevision}");
        _ = await taskRuns.EnqueueAsync(
            request,
            cancellationToken).ConfigureAwait(false);
    }
}
