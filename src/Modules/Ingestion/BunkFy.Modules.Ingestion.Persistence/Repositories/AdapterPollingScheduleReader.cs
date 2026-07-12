namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using Microsoft.EntityFrameworkCore;

internal sealed class AdapterPollingScheduleReader(IngestionDbContext dbContext)
    : IAdapterPollingScheduleReader
{
    public async Task<IReadOnlyList<AdapterPollingScheduleDefinition>> ListActiveAsync(
        CancellationToken cancellationToken) =>
        await dbContext.AdapterConnections
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
                connection.PollingScheduleMaxAttempts!.Value))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
}
