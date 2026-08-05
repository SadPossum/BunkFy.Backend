namespace BunkFy.Extensions.DataRights.TenantTermination.Tests;

using BunkFy.Modules.DataRights.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationProductionAdmissionTests
{
    private static readonly string Digest = new('a', 64);

    [Fact]
    public void Disabled_policy_preserves_partial_worker_composition()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Production
            });

        Exception? exception = Record.Exception(() =>
            builder.AddBunkFyTenantTerminationProductionAdmission(
                dataRightsComposed: false,
                completeOwnerTopology: false,
                taskWorkerEnabled: false));

        Assert.Null(exception);
    }

    [Fact]
    public void Enabled_policy_requires_runtime_and_production_evidence()
    {
        HostApplicationBuilder incomplete = Builder(
            Environments.Production,
            includeEvidence: false,
            includeWorkerGroup: false);

        OptionsValidationException exception = Assert.Throws<
            OptionsValidationException>(() =>
            incomplete.AddBunkFyTenantTerminationProductionAdmission(
                dataRightsComposed: false,
                completeOwnerTopology: false,
                taskWorkerEnabled: false));

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains(
                "complete owner topology",
                StringComparison.Ordinal));
        Assert.Contains(
            exception.Failures,
            failure => failure.Contains(
                "OwnerCatalogSha256",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Exact_catalog_and_replay_readiness_admit_execution()
    {
        HostApplicationBuilder builder = Builder(
            Environments.Production,
            includeEvidence: true,
            includeWorkerGroup: true);
        builder.Services.AddSingleton<
            ITenantTerminationProductionReadinessProbe>(
            new StubReadinessProbe());
        builder.AddBunkFyTenantTerminationProductionAdmission(
            dataRightsComposed: true,
            completeOwnerTopology: true,
            taskWorkerEnabled: true);
        using IHost host = builder.Build();
        ITenantTerminationRequiredOwnerCatalog requiredOwners = host.Services
            .GetRequiredService<ITenantTerminationRequiredOwnerCatalog>();
        IHostedService admission = host.Services.GetServices<IHostedService>()
            .Single(service => service.GetType().Name ==
                "TenantTerminationProductionAdmissionStartupValidator");

        await admission.StartAsync(CancellationToken.None);
        Assert.NotEmpty(requiredOwners.RequiredOwnerKeys);
    }

    [Fact]
    public async Task Catalog_drift_and_non_durable_replay_fail_closed()
    {
        HostApplicationBuilder drifted = Builder(
            Environments.Production,
            includeEvidence: true,
            includeWorkerGroup: true);
        drifted.Services.AddSingleton<
            ITenantTerminationProductionReadinessProbe>(
            new StubReadinessProbe(new string('b', 64)));
        drifted.AddBunkFyTenantTerminationProductionAdmission(
            true,
            true,
            true);
        using IHost driftedHost = drifted.Build();
        IHostedService driftedAdmission = driftedHost.Services
            .GetServices<IHostedService>()
            .Single(service => service.GetType().Name ==
                "TenantTerminationProductionAdmissionStartupValidator");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driftedAdmission.StartAsync(CancellationToken.None));

        HostApplicationBuilder nonDurable = Builder(
            Environments.Production,
            includeEvidence: true,
            includeWorkerGroup: true);
        nonDurable.Services.AddSingleton<
            ITenantTerminationProductionReadinessProbe>(
            new StubReadinessProbe(isProductionGrade: false));
        nonDurable.AddBunkFyTenantTerminationProductionAdmission(
            true,
            true,
            true);
        using IHost nonDurableHost = nonDurable.Build();
        IHostedService nonDurableAdmission = nonDurableHost.Services
            .GetServices<IHostedService>()
            .Single(service => service.GetType().Name ==
                "TenantTerminationProductionAdmissionStartupValidator");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            nonDurableAdmission.StartAsync(CancellationToken.None));
    }

    private static HostApplicationBuilder Builder(
        string environmentName,
        bool includeEvidence,
        bool includeWorkerGroup)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = environmentName
            });
        ConfigurationManager configuration = builder.Configuration;
        string section = TenantTerminationProductionAdmissionOptions.SectionName;
        configuration[$"{section}:ExecutionEnabled"] = "true";
        if (includeWorkerGroup)
        {
            configuration["Tasks:Worker:WorkerGroups:0"] =
                "tenant-termination-workers";
        }

        if (includeEvidence)
        {
            configuration[$"{section}:ApprovalState"] = "Approved";
            configuration[$"{section}:ApprovalReference"] = "change:42";
            configuration[$"{section}:OwnerCatalogSha256"] = Digest;
            configuration[$"{section}:BackupEvidenceReference"] =
                "backup:42";
            configuration[$"{section}:RestoreDrillEvidenceReference"] =
                "restore:42";
            configuration[$"{section}:OperatorAssuranceReference"] =
                "assurance:42";
        }

        return builder;
    }

    private sealed class StubReadinessProbe(
        string? digest = null,
        bool isProductionGrade = true)
        : ITenantTerminationProductionReadinessProbe
    {
        public Task<TenantTerminationProductionReadinessEvidence> CheckAsync(
            IReadOnlyCollection<string> requiredOwnerKeys,
            CancellationToken cancellationToken) =>
            Task.FromResult(new TenantTerminationProductionReadinessEvidence(
                IsCatalogValid: true,
                OwnerCount: requiredOwnerKeys.Count,
                ExportOwnerCount: 9,
                TerminalOwnerKey: "workspaces",
                CatalogSha256: digest ?? Digest,
                ReplayStoreProvider: "test",
                IsReplayStoreReady: true,
                IsReplayStoreProductionGrade: isProductionGrade,
                ReplayStoreFailureCode: null));
    }
}
