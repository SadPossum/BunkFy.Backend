namespace BunkFy.Modules.DataRights.Application.Tasks;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.Tasks;

[IntegrationEventHandler(
    DataRightsModuleMetadata.AnonymisationExecutionPreparedHandlerName)]
internal sealed class DataRightsAnonymisationExecutionPreparedHandler(
    ITaskRunStore taskRuns)
    : IIntegrationEventHandler<
        DataRightsAnonymisationExecutionPreparedIntegrationEvent>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public Task HandleAsync(
        DataRightsAnonymisationExecutionPreparedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ExecuteDataRightsAnonymisationPayload payload = new(
            integrationEvent.WorkItemId,
            integrationEvent.CaseId,
            integrationEvent.PropertyId,
            integrationEvent.ApprovalRevision,
            integrationEvent.ExecutionRevision);
        TaskRunRequest request = new(
            integrationEvent.EventId,
            DataRightsModuleMetadata.Name,
            ExecuteDataRightsAnonymisationPayload.TaskName,
            JsonSerializer.Serialize(payload, SerializerOptions),
            integrationEvent.OccurredAtUtc,
            integrationEvent.OccurredAtUtc,
            DataRightsModuleMetadata.AnonymisationWorkerGroup,
            integrationEvent.TenantId,
            integrationEvent.CaseId,
            requestedBy: "system:data-rights",
            maxAttempts: 5,
            payloadVersion: ExecuteDataRightsAnonymisationPayload.PayloadVersion,
            deduplicationKey:
                $"{integrationEvent.WorkItemId:N}:{integrationEvent.ExecutionRevision}");
        return EnqueueAsync(taskRuns, request, cancellationToken);
    }

    private static async Task EnqueueAsync(
        ITaskRunStore taskRuns,
        TaskRunRequest request,
        CancellationToken cancellationToken)
    {
        _ = await taskRuns.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
