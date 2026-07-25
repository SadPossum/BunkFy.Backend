namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;
using Microsoft.Extensions.Diagnostics.HealthChecks;

internal sealed class DataRightsRestoreReadinessHealthCheck(
    IDataRightsRestoreReadiness readiness)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        DataRightsRestoreReadinessSnapshot snapshot =
            readiness.Snapshot;
        return Task.FromResult(snapshot.IsReady
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(snapshot.StatusCode));
    }
}
