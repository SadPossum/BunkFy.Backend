namespace BunkFy.Host.ServiceDefaults.Tests.Production;

using BunkFy.Host.ServiceDefaults.Production;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class BunkFyDurableRuntimeProductionAdmissionValidatorTests
{
    [Fact]
    public void Production_rejects_an_unapproved_or_ownerless_policy()
    {
        BunkFyDurableRuntimeProductionAdmissionOptions options = new();

        string[] failures = Validate(
            options,
            CreateRegistration(BunkFyDurableRuntimeHostRole.PublicApi),
            CreateRuntime(ownsMaintenance: false));

        Assert.Contains(failures, failure => failure.Contains("ApprovalState", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("ApprovalReference", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("MaintenanceOwner", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("MaintenanceOwnerInstanceCount", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_accepts_an_approved_non_owner_public_api()
    {
        string[] failures = Validate(
            CreateApprovedOptions(ownsMaintenance: false),
            CreateRegistration(BunkFyDurableRuntimeHostRole.PublicApi),
            CreateRuntime(ownsMaintenance: false));

        Assert.Empty(failures);
    }

    [Fact]
    public void Production_accepts_one_exact_worker_maintenance_owner()
    {
        string[] failures = Validate(
            CreateApprovedOptions(ownsMaintenance: true),
            CreateRegistration(
                BunkFyDurableRuntimeHostRole.Worker,
                taskRuntimeComposed: true,
                taskWorkerEnabled: true,
                taskSchedulerEnabled: true,
                natsPublishingEnabled: true,
                natsConsumersEnabled: true),
            CreateRuntime(ownsMaintenance: true));

        Assert.Empty(failures);
    }

    [Fact]
    public void Maintenance_owner_must_be_a_worker_with_task_runtime_and_both_cleanups()
    {
        BunkFyDurableRuntimeProductionAdmissionOptions options =
            CreateApprovedOptions(ownsMaintenance: true);
        BunkFyDurableRuntimeRuntimeOptions runtime =
            CreateRuntime(ownsMaintenance: true) with
            {
                CleanupProcessedInbox = false
            };

        string[] failures = Validate(
            options,
            CreateRegistration(
                BunkFyDurableRuntimeHostRole.AdminApi,
                taskRuntimeComposed: false),
            runtime);

        Assert.Contains(failures, failure => failure.Contains("Only a BunkFy Worker", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("must compose TaskRuntime", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("both processed outbox and inbox", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("TaskRuntimeRetention:Enabled requires", StringComparison.Ordinal));
    }

    [Fact]
    public void Non_owner_process_rejects_enabled_cleanup()
    {
        string[] failures = Validate(
            CreateApprovedOptions(ownsMaintenance: false),
            CreateRegistration(
                BunkFyDurableRuntimeHostRole.Worker,
                taskRuntimeComposed: true),
            CreateRuntime(ownsMaintenance: true));

        Assert.Contains(failures, failure => failure.Contains("MessageJournalCleanup:Enabled must be false", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("TaskRuntimeRetention:Enabled must be false", StringComparison.Ordinal));
    }

    [Fact]
    public void Task_and_broker_modes_require_their_runtime_dependencies()
    {
        string[] failures = Validate(
            CreateApprovedOptions(ownsMaintenance: false),
            CreateRegistration(
                BunkFyDurableRuntimeHostRole.Worker,
                taskWorkerEnabled: true,
                taskSchedulerEnabled: true,
                natsConsumersEnabled: true),
            CreateRuntime(ownsMaintenance: false));

        Assert.Contains(failures, failure => failure.Contains("Tasks:Worker:Enabled requires", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("Tasks:Scheduler:Enabled requires", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("NatsConsumers:Enabled requires", StringComparison.Ordinal));
    }

    [Fact]
    public void Approved_windows_must_be_bounded_and_match_runtime()
    {
        BunkFyDurableRuntimeProductionAdmissionOptions options =
            CreateApprovedOptions(ownsMaintenance: true);
        options.ProcessedInboxRetention = TimeSpan.FromDays(1);
        options.BrokerReplayHorizon = TimeSpan.FromDays(2);
        options.FailedRunRetention = TimeSpan.Zero;

        string[] failures = Validate(
            options,
            CreateRegistration(
                BunkFyDurableRuntimeHostRole.Worker,
                taskRuntimeComposed: true),
            CreateRuntime(ownsMaintenance: true));

        Assert.Contains(failures, failure => failure.Contains("FailedRunRetention must be positive", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("greater than or equal to BrokerReplayHorizon", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("ProcessedInboxRetention must equal", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("FailedRunRetention must equal", StringComparison.Ordinal));
    }

    [Fact]
    public void Non_production_does_not_require_runtime_approval()
    {
        string[] failures = Validate(
            new BunkFyDurableRuntimeProductionAdmissionOptions(),
            CreateRegistration(
                BunkFyDurableRuntimeHostRole.Worker,
                isProduction: false,
                taskWorkerEnabled: true,
                natsConsumersEnabled: true),
            CreateRuntime(ownsMaintenance: false));

        Assert.Empty(failures);
    }

    [Fact]
    public void Production_composition_rejects_pending_admission()
    {
        HostApplicationBuilder builder = CreateProductionBuilder();

        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(() =>
                builder.AddBunkFyDurableRuntimeProductionAdmission(
                    BunkFyDurableRuntimeHostRole.PublicApi,
                    taskRuntimeComposed: false));

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("ApprovalState", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_composition_binds_an_approved_non_owner_policy()
    {
        HostApplicationBuilder builder = CreateProductionBuilder();
        ConfigureApprovedAdmission(builder, ownsMaintenance: false);

        builder.AddBunkFyDurableRuntimeProductionAdmission(
            BunkFyDurableRuntimeHostRole.PublicApi,
            taskRuntimeComposed: false);

        using IHost host = builder.Build();
        BunkFyDurableRuntimeProductionAdmissionOptions options = host.Services
            .GetRequiredService<IOptions<
                BunkFyDurableRuntimeProductionAdmissionOptions>>()
            .Value;
        Assert.Equal(
            BunkFyDurableRuntimeApprovalState.Approved,
            options.ApprovalState);
        Assert.False(options.CurrentProcessOwnsMaintenance);
    }

    private static string[] Validate(
        BunkFyDurableRuntimeProductionAdmissionOptions options,
        BunkFyDurableRuntimeProductionAdmissionRegistration registration,
        BunkFyDurableRuntimeRuntimeOptions runtime)
    {
        ValidateOptionsResult result =
            new BunkFyDurableRuntimeProductionAdmissionValidator(
                registration,
                runtime)
                .Validate(name: null, options);
        return result.Failed
            ? result.Failures.ToArray()
            : [];
    }

    private static BunkFyDurableRuntimeProductionAdmissionOptions
        CreateApprovedOptions(bool ownsMaintenance) =>
        new()
        {
            ApprovalState = BunkFyDurableRuntimeApprovalState.Approved,
            ApprovalReference = "ops/runtime-approval-2026-08",
            MaintenanceOwner = BunkFyDurableRuntimeMaintenanceOwner.Worker,
            MaintenanceOwnerInstanceCount = 1,
            CurrentProcessOwnsMaintenance = ownsMaintenance
        };

    private static BunkFyDurableRuntimeProductionAdmissionRegistration
        CreateRegistration(
            BunkFyDurableRuntimeHostRole role,
            bool isProduction = true,
            bool taskRuntimeComposed = false,
            bool taskWorkerEnabled = false,
            bool taskSchedulerEnabled = false,
            bool natsPublishingEnabled = false,
            bool natsConsumersEnabled = false) =>
        new(
            isProduction,
            role,
            taskRuntimeComposed,
            taskWorkerEnabled,
            taskSchedulerEnabled,
            natsPublishingEnabled,
            natsConsumersEnabled);

    private static BunkFyDurableRuntimeRuntimeOptions CreateRuntime(
        bool ownsMaintenance) =>
        new(
            ownsMaintenance,
            CleanupProcessedOutbox: true,
            CleanupProcessedInbox: true,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(14),
            TimeSpan.FromDays(7),
            ownsMaintenance,
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(90),
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(90),
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(90),
            TimeSpan.FromDays(30));

    private static HostApplicationBuilder CreateProductionBuilder() =>
        Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Production
            });

    private static void ConfigureApprovedAdmission(
        HostApplicationBuilder builder,
        bool ownsMaintenance)
    {
        string section =
            BunkFyDurableRuntimeProductionAdmissionOptions.SectionName;
        builder.Configuration[$"{section}:ApprovalState"] = "Approved";
        builder.Configuration[$"{section}:ApprovalReference"] =
            "ops/runtime-approval-2026-08";
        builder.Configuration[$"{section}:MaintenanceOwner"] = "Worker";
        builder.Configuration[$"{section}:MaintenanceOwnerInstanceCount"] = "1";
        builder.Configuration[$"{section}:CurrentProcessOwnsMaintenance"] =
            ownsMaintenance.ToString();
    }
}
