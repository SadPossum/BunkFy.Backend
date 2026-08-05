namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Runtime.CompilerServices;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using Microsoft.EntityFrameworkCore;

internal sealed class AdapterPollingScheduleReader(IngestionDbContext dbContext)
    : IAdapterPollingScheduleReader
{
    public async Task<IReadOnlyList<AdapterPollingScheduleDefinition>> ListActiveAsync(
        CancellationToken cancellationToken) =>
        await this.ActiveSchedules()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public async IAsyncEnumerable<AdapterPollingScheduleDefinition> StreamActiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (AdapterPollingScheduleDefinition schedule in this
            .ActiveSchedules()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return schedule;
        }
    }

    private IQueryable<AdapterPollingScheduleDefinition> ActiveSchedules() =>
        dbContext.AdapterConnections
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(connection =>
                connection.State == AdapterConnectionState.Enabled &&
                connection.ExecutionMode == BunkFy.Adapter.Abstractions.AdapterExecutionMode.Polling &&
                connection.PollingIntervalSeconds != null &&
                connection.PollingScheduleMaxAttempts != null)
            .OrderBy(connection => connection.ScopeId)
            .ThenBy(connection => connection.Id)
            .Select(connection => new AdapterPollingScheduleDefinition(
                connection.ScopeId,
                connection.Id,
                connection.PollingIntervalSeconds!.Value,
                connection.PollingScheduleMaxAttempts!.Value));
}
