namespace BunkFy.Modules.Ingestion.Tests.Application;

using System.Text.Json;
using Gma.Framework.Tasks;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Tasks;
using BunkFy.Modules.Ingestion.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionPollingScheduleProviderTests
{
    [Fact]
    public async Task Provider_maps_connection_owned_schedules_to_scoped_deduplicated_tasks()
    {
        Guid connectionId = Guid.NewGuid();
        IngestionPollingScheduleProvider provider = new(new FakeReader(
        [
            new AdapterPollingScheduleDefinition("tenant-b", connectionId, 300, 4)
        ]));

        ScheduledTaskDefinition schedule = Assert.Single(
            await provider.GetSchedulesAsync(CancellationToken.None));

        Assert.Equal($"adapter-{connectionId:N}", schedule.ScheduleName);
        Assert.Equal(IngestionModuleMetadata.Name, schedule.ModuleName);
        Assert.Equal(RunAdapterTaskPayload.TaskName, schedule.TaskName);
        Assert.Equal(IngestionModuleMetadata.AdapterWorkerGroup, schedule.WorkerGroup);
        Assert.Equal("tenant-b", schedule.ScopeId);
        Assert.Equal(TimeSpan.FromMinutes(5), schedule.Interval);
        Assert.Equal(4, schedule.MaxAttempts);
        Assert.True(schedule.RunOnStart);
        Assert.Contains(connectionId.ToString("N"), schedule.DeduplicationKeyPrefix, StringComparison.Ordinal);
        using JsonDocument payload = JsonDocument.Parse(schedule.PayloadJson);
        Assert.Equal(connectionId, payload.RootElement.GetProperty("ConnectionId").GetGuid());
    }

    private sealed class FakeReader(IReadOnlyList<AdapterPollingScheduleDefinition> schedules)
        : IAdapterPollingScheduleReader
    {
        public Task<IReadOnlyList<AdapterPollingScheduleDefinition>> ListActiveAsync(
            CancellationToken cancellationToken) => Task.FromResult(schedules);
    }
}
