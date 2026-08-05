namespace BunkFy.Extensions.DataRights.TenantTermination;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Production;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Results;

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
        ITenantTerminationProductionCatalog catalog = scope.ServiceProvider
            .GetRequiredService<ITenantTerminationProductionCatalog>();
        ITenantTerminationReplayStore replayStore = scope.ServiceProvider
            .GetRequiredService<ITenantTerminationReplayStore>();
        Result<TenantTerminationProductionCatalogEvidence> evidence =
            catalog.Validate(
                TenantTerminationProductionOwnerCatalog.RequiredOwnerKeys);
        if (evidence.IsFailure)
        {
            throw new InvalidOperationException(
                "The tenant-termination owner catalogue is incomplete or invalid.");
        }

        if (registration.IsProduction && !string.Equals(
                admission.OwnerCatalogSha256,
                evidence.Value.CatalogSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The admitted tenant-termination owner catalogue digest does not match the composed topology.");
        }

        TenantTerminationReplayStoreReadiness readiness =
            await replayStore.CheckReadinessAsync(cancellationToken)
                .ConfigureAwait(false);
        if (!readiness.IsReady ||
            (registration.IsProduction && !readiness.IsProductionGrade))
        {
            throw new InvalidOperationException(
                "The admitted tenant-termination replay store is not ready for this environment.");
        }

        logger.LogInformation(
            "Tenant-termination execution admitted with {OwnerCount} owners, {ExportOwnerCount} export owners, terminal owner {TerminalOwnerKey}, and catalogue {CatalogSha256}.",
            evidence.Value.OwnerCount,
            evidence.Value.ExportOwnerCount,
            evidence.Value.TerminalOwnerKey,
            evidence.Value.CatalogSha256);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
