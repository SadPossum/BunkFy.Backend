namespace BunkFy.Modules.DataRights.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

internal sealed class DataRightsRestoreHealthCheckOptionsSetup
    : IConfigureOptions<HealthCheckServiceOptions>
{
    internal const string RegistrationName =
        "data-rights-restore-readiness";

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
                DataRightsRestoreReadinessHealthCheck>(),
            HealthStatus.Unhealthy,
            tags: null));
    }
}
