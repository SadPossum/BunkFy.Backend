namespace BunkFy.Modules.DataRights.Application.Tasks;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tasks;

[IntegrationEventHandler(DataRightsModuleMetadata.ExportArtifactRequestedHandlerName)]
internal sealed class DataRightsExportArtifactRequestedHandler(
    ITaskRunStore taskRuns,
    IIdGenerator ids)
    : IIntegrationEventHandler<DataRightsExportArtifactRequestedIntegrationEvent>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(
        DataRightsExportArtifactRequestedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        GenerateDataRightsExportPayload payload = new(
            integrationEvent.ArtifactId,
            integrationEvent.CaseId,
            integrationEvent.CaseType,
            integrationEvent.PropertyId,
            integrationEvent.DecisionRevision);
        TaskRunRequest request = new(
            integrationEvent.EventId,
            DataRightsModuleMetadata.Name,
            GenerateDataRightsExportPayload.TaskName,
            JsonSerializer.Serialize(payload, SerializerOptions),
            integrationEvent.OccurredAtUtc,
            integrationEvent.OccurredAtUtc,
            DataRightsModuleMetadata.ExportWorkerGroup,
            integrationEvent.TenantId,
            integrationEvent.CaseId,
            requestedBy: "system:data-rights-export",
            maxAttempts: 5,
            payloadVersion: GenerateDataRightsExportPayload.PayloadVersion,
            deduplicationKey:
                $"{integrationEvent.ArtifactId:N}:" +
                $"{integrationEvent.DecisionRevision}:" +
                $"{integrationEvent.EventId:N}");
        _ = await taskRuns.EnqueueAsync(request, cancellationToken)
            .ConfigureAwait(false);

        DeleteExpiredDataRightsExportArtifactPayload cleanupPayload = new(
            integrationEvent.ArtifactId,
            integrationEvent.CaseId,
            integrationEvent.CaseType,
            integrationEvent.PropertyId,
            integrationEvent.DecisionRevision,
            integrationEvent.ExpiresAtUtc);
        TaskRunRequest cleanup = new(
            ids.NewId(),
            DataRightsModuleMetadata.Name,
            DeleteExpiredDataRightsExportArtifactPayload.TaskName,
            JsonSerializer.Serialize(cleanupPayload, SerializerOptions),
            integrationEvent.OccurredAtUtc,
            integrationEvent.ExpiresAtUtc,
            DataRightsModuleMetadata.ExportWorkerGroup,
            integrationEvent.TenantId,
            integrationEvent.CaseId,
            requestedBy: "system:data-rights-export-retention",
            maxAttempts: 10,
            payloadVersion:
                DeleteExpiredDataRightsExportArtifactPayload.PayloadVersion,
            deduplicationKey:
                $"{integrationEvent.ArtifactId:N}:" +
                $"{integrationEvent.ExpiresAtUtc.UtcTicks}");
        _ = await taskRuns.EnqueueAsync(cleanup, cancellationToken)
            .ConfigureAwait(false);
    }
}
