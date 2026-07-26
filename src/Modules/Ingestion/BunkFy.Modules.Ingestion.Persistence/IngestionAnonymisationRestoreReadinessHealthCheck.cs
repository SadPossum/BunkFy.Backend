namespace BunkFy.Modules.Ingestion.Persistence;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

internal sealed class IngestionAnonymisationRestoreReadinessHealthCheck(
    IServiceScopeFactory scopeFactory)
    : IHealthCheck
{
    internal const string IncompleteRestoreCode =
        "ingestion.anonymisation-restore.incomplete";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IngestionDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<IngestionDbContext>();
        bool hasIncompleteRestore = await dbContext
            .AnonymisationTombstones
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                tombstone =>
                    tombstone.State ==
                    IngestionAnonymisationTombstoneState.Reducing,
                cancellationToken)
            .ConfigureAwait(false);

        return hasIncompleteRestore
            ? HealthCheckResult.Unhealthy(IncompleteRestoreCode)
            : HealthCheckResult.Healthy();
    }
}
