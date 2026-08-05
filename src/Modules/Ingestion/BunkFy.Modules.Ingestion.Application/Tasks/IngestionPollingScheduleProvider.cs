namespace BunkFy.Modules.Ingestion.Application.Tasks;

using System.Runtime.CompilerServices;
using System.Text.Json;
using Gma.Framework.Tasks;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;

internal sealed class IngestionPollingScheduleProvider(IAdapterPollingScheduleReader schedules)
    : ITaskScheduleProvider
{
    public async IAsyncEnumerable<ScheduledTaskDefinition> GetSchedulesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (AdapterPollingScheduleDefinition schedule in schedules
            .StreamActiveAsync(cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new ScheduledTaskDefinition(
                $"adapter-{schedule.ConnectionId:N}",
                IngestionModuleMetadata.Name,
                RunAdapterTaskPayload.TaskName,
                JsonSerializer.Serialize(new RunAdapterTaskPayload(schedule.ConnectionId)),
                TimeSpan.FromSeconds(schedule.IntervalSeconds),
                IngestionModuleMetadata.AdapterWorkerGroup,
                schedule.ScopeId,
                schedule.MaxAttempts,
                RunAdapterTaskPayload.PayloadVersion,
                runOnStart: true);
        }
    }
}
