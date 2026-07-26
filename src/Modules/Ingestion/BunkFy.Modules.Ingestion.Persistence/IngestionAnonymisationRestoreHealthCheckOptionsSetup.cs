namespace BunkFy.Modules.Ingestion.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class IngestionAnonymisationRestoreHealthCheckOptionsSetup
    : IConfigureOptions<HealthCheckServiceOptions>
{
    internal const string RegistrationName =
        "ingestion-anonymisation-restore-readiness";

    public void Configure(HealthCheckServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Registrations.Any(registration =>
                string.Equals(
                    registration.Name,
                    RegistrationName,
                    StringComparison.Ordinal)))
        {
            return;
        }

        options.Registrations.Add(new HealthCheckRegistration(
            RegistrationName,
            services => services.GetRequiredService<
                IngestionAnonymisationRestoreReadinessHealthCheck>(),
            HealthStatus.Unhealthy,
            tags: null));
    }
}
