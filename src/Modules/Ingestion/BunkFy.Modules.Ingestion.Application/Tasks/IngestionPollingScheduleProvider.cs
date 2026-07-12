namespace BunkFy.Modules.Ingestion.Application.Tasks;

using System.Text.Json;
using Gma.Framework.Tasks;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;

internal sealed class IngestionPollingScheduleProvider(IAdapterPollingScheduleReader schedules)
    : ITaskScheduleProvider
{
    public async Task<IReadOnlyList<ScheduledTaskDefinition>> GetSchedulesAsync(
        CancellationToken cancellationToken) =>
        (await schedules.ListActiveAsync(cancellationToken).ConfigureAwait(false))
        .Select(schedule => new ScheduledTaskDefinition(
            $"adapter-{schedule.ConnectionId:N}",
            IngestionModuleMetadata.Name,
            RunAdapterTaskPayload.TaskName,
            JsonSerializer.Serialize(new RunAdapterTaskPayload(schedule.ConnectionId)),
            TimeSpan.FromSeconds(schedule.IntervalSeconds),
            IngestionModuleMetadata.AdapterWorkerGroup,
            schedule.ScopeId,
            schedule.MaxAttempts,
            RunAdapterTaskPayload.PayloadVersion,
            runOnStart: true))
        .ToArray();
}
