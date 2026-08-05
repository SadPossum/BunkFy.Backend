namespace BunkFy.Extensions.DataRights.TenantTermination;

using BunkFy.Modules.DataRights.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class TenantTerminationProductionAdmissionStartupValidator(
    IServiceScopeFactory scopeFactory,
    IOptions<TenantTerminationProductionAdmissionOptions> options,
    TenantTerminationProductionAdmissionRegistration registration,
    ILogger<TenantTerminationProductionAdmissionStartupValidator> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        TenantTerminationProductionAdmissionOptions admission = options.Value;
        if (!admission.ExecutionEnabled)
        {
            logger.LogInformation(
                "Tenant-termination execution is disabled by production admission policy.");
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        ITenantTerminationProductionReadinessProbe probe = scope.ServiceProvider
            .GetRequiredService<ITenantTerminationProductionReadinessProbe>();
        TenantTerminationProductionReadinessEvidence evidence =
            await probe.CheckAsync(
                    TenantTerminationProductionOwnerCatalog.RequiredOwnerKeys,
                    cancellationToken)
                .ConfigureAwait(false);
        if (!evidence.IsCatalogValid)
        {
            throw new InvalidOperationException(
                "The tenant-termination owner catalogue is incomplete or invalid.");
        }

        if (registration.IsProduction && !string.Equals(
                admission.OwnerCatalogSha256,
                evidence.CatalogSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The admitted tenant-termination owner catalogue digest does not match the composed topology.");
        }

        if (!evidence.IsReplayStoreReady ||
            (registration.IsProduction &&
             !evidence.IsReplayStoreProductionGrade))
        {
            throw new InvalidOperationException(
                "The admitted tenant-termination replay store is not ready for this environment.");
        }

        logger.LogInformation(
            "Tenant-termination execution admitted with {OwnerCount} owners, {ExportOwnerCount} export owners, terminal owner {TerminalOwnerKey}, and catalogue {CatalogSha256}.",
            evidence.OwnerCount,
            evidence.ExportOwnerCount,
            evidence.TerminalOwnerKey,
            evidence.CatalogSha256);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
