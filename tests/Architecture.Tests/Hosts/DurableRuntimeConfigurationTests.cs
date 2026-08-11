namespace Architecture.Tests.Hosts;

using System.Globalization;
using System.Text.Json;
using Architecture.Tests.Support;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class DurableRuntimeConfigurationTests
{
    private static readonly string[] MessagingHosts =
    [
        "BunkFy.Host.Api",
        "BunkFy.Host.AdminApi",
        "BunkFy.Host.AdminCli",
        "BunkFy.Host.Worker",
    ];

    private static readonly string[] AuthRetentionHosts =
    [
        "BunkFy.Host.Api",
        "BunkFy.Host.AdminApi",
        "BunkFy.Host.Worker",
    ];

    [Fact]
    public void Messaging_hosts_expose_bounded_replay_safe_runtime_defaults()
    {
        foreach (string host in MessagingHosts)
        {
            using JsonDocument document = JsonDocument.Parse(
                RepositoryPaths.Read("src", host, "appsettings.json"));
            JsonElement root = document.RootElement;
            JsonElement cleanup = root.GetProperty("MessageJournalCleanup");
            JsonElement jetStream = root.GetProperty("NatsJetStream");
            JsonElement consumers = root.GetProperty("NatsConsumers");

            Assert.False(cleanup.GetProperty("Enabled").GetBoolean());
            Assert.True(ParseDuration(cleanup, "ProcessedInboxRetention") >=
                        ParseDuration(cleanup, "BrokerReplayHorizon"));
            Assert.True(cleanup.GetProperty("BatchSize").GetInt32() > 0);
            Assert.True(cleanup.GetProperty("MaxBatchesPerStorePerCycle").GetInt32() > 0);

            Assert.Equal("Managed", jetStream.GetProperty("ManagementMode").GetString());
            Assert.Equal("File", jetStream.GetProperty("Storage").GetString());
            Assert.True(ParseDuration(jetStream, "MaxAge") > TimeSpan.Zero);
            Assert.True(jetStream.GetProperty("MaxBytes").GetInt64() > 0);
            Assert.True(jetStream.GetProperty("MaxMessages").GetInt64() > 0);
            Assert.True(jetStream.GetProperty("MaxMessageSize").GetInt32() > 0);
            Assert.True(jetStream.GetProperty("Replicas").GetInt32() > 0);
            Assert.Equal("Old", jetStream.GetProperty("DiscardPolicy").GetString());

            Assert.True(ParseDuration(consumers, "AckProgressInterval") <
                        ParseDuration(consumers, "AckWait"));
        }
    }

    [Fact]
    public void Worker_exposes_lease_heartbeat_and_task_history_retention_defaults()
    {
        using JsonDocument document = JsonDocument.Parse(
            RepositoryPaths.Read("src", "BunkFy.Host.Worker", "appsettings.json"));
        JsonElement root = document.RootElement;
        JsonElement worker = root.GetProperty("Tasks").GetProperty("Worker");
        JsonElement retention = root.GetProperty("TaskRuntimeRetention");

        Assert.True(ParseDuration(worker, "HeartbeatInterval") < ParseDuration(worker, "LeaseDuration"));
        Assert.False(retention.GetProperty("Enabled").GetBoolean());
        Assert.True(retention.GetProperty("BatchSize").GetInt32() > 0);
        Assert.True(retention.GetProperty("MaxBatchesPerStatusPerCycle").GetInt32() > 0);
    }

    [Fact]
    public void Long_running_hosts_expose_fail_closed_durable_runtime_admission_defaults()
    {
        string[] hosts =
        [
            "BunkFy.Host.Api",
            "BunkFy.Host.AdminApi",
            "BunkFy.Host.Worker"
        ];

        foreach (string host in hosts)
        {
            using JsonDocument document = JsonDocument.Parse(
                RepositoryPaths.Read("src", host, "appsettings.json"));
            JsonElement admission = document.RootElement
                .GetProperty("BunkFy")
                .GetProperty("DurableRuntime")
                .GetProperty("ProductionAdmission");

            Assert.Equal("Pending", admission.GetProperty("ApprovalState").GetString());
            Assert.Equal("Unspecified", admission.GetProperty("MaintenanceOwner").GetString());
            Assert.Equal(0, admission.GetProperty("MaintenanceOwnerInstanceCount").GetInt32());
            Assert.False(admission.GetProperty("CurrentProcessOwnsMaintenance").GetBoolean());
        }
    }

    [Fact]
    public void Production_hosts_compose_durable_runtime_admission_at_their_actual_boundaries()
    {
        string publicApi = RepositoryPaths.Read("src", "BunkFy.Host.Api", "Program.cs");
        string adminApi = RepositoryPaths.Read("src", "BunkFy.Host.AdminApi", "Program.cs");
        string worker = RepositoryPaths.Read(
            "src",
            "BunkFy.Host.Worker",
            "WorkerHostBuilderExtensions.cs");

        Assert.Contains(
            "BunkFyDurableRuntimeHostRole.PublicApi",
            publicApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "BunkFyDurableRuntimeHostRole.AdminApi",
            adminApi,
            StringComparison.Ordinal);
        Assert.Contains(
            "BunkFyDeploymentSurface.Worker",
            worker,
            StringComparison.Ordinal);
        Assert.Contains(
            "BunkFyDurableRuntimeHostRole.Worker",
            worker,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Long_running_hosts_expose_fail_closed_identity_maintenance_admission_defaults()
    {
        foreach (string host in AuthRetentionHosts)
        {
            using JsonDocument document = JsonDocument.Parse(
                RepositoryPaths.Read("src", host, "appsettings.json"));
            JsonElement bunkFy = document.RootElement.GetProperty("BunkFy");

            AssertFailClosedMaintenanceAdmission(
                bunkFy.GetProperty("AuthRetention").GetProperty("ProductionAdmission"));
            AssertFailClosedMaintenanceAdmission(
                bunkFy.GetProperty("OrganizationsMaintenance").GetProperty("ProductionAdmission"));
        }
    }

    [Fact]
    public void Production_hosts_compose_identity_maintenance_admission_at_actual_boundaries()
    {
        string publicApi = RepositoryPaths.Read("src", "BunkFy.Host.Api", "Program.cs");
        string adminApi = RepositoryPaths.Read("src", "BunkFy.Host.AdminApi", "Program.cs");
        string worker = RepositoryPaths.Read(
            "src",
            "BunkFy.Host.Worker",
            "WorkerHostBuilderExtensions.cs");

        foreach (string source in new[] { publicApi, adminApi, worker })
        {
            Assert.Contains(
                "AddBunkFyAuthRetentionProductionAdmission",
                source,
                StringComparison.Ordinal);
            Assert.Contains(
                "AddBunkFyOrganizationsMaintenanceProductionAdmission",
                source,
                StringComparison.Ordinal);
        }

        Assert.Contains("workerOptions.Modules.Auth", worker, StringComparison.Ordinal);
        Assert.Contains("workerOptions.Modules.Organizations", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void Organizations_expiry_and_retention_are_bounded_and_owned_by_the_worker_composition()
    {
        foreach (string host in AuthRetentionHosts)
        {
            using JsonDocument document = JsonDocument.Parse(
                RepositoryPaths.Read("src", host, "appsettings.json"));
            JsonElement organizations = document.RootElement.GetProperty("Organizations");
            JsonElement lifecycle = organizations.GetProperty("Lifecycle");
            JsonElement retention = organizations.GetProperty("Retention");

            Assert.False(lifecycle.GetProperty("Enabled").GetBoolean());
            Assert.True(lifecycle.GetProperty("BatchSize").GetInt32() > 0);
            Assert.True(lifecycle.GetProperty("MaxBatchesPerCategoryPerCycle").GetInt32() > 0);
            Assert.True(lifecycle.GetProperty("IntervalMinutes").GetInt32() > 0);
            Assert.False(retention.GetProperty("Enabled").GetBoolean());
            Assert.InRange(retention.GetProperty("InvitationHistoryDays").GetInt32(), 1, 3650);
            Assert.InRange(retention.GetProperty("EnrollmentHistoryDays").GetInt32(), 1, 3650);
        }

        using JsonDocument apiDocument = JsonDocument.Parse(
            RepositoryPaths.Read("src", "BunkFy.Host.Api", "appsettings.json"));
        Assert.Equal(
            168,
            apiDocument.RootElement
                .GetProperty("Organizations")
                .GetProperty("EnrollmentClaimLifetimeHours")
                .GetInt32());

        string composition = RepositoryPaths.Read(
            "src",
            "Shared",
            "BunkFy.AppHost.Composition",
            "BunkFyBackendComposition.cs");
        Assert.Contains(
            ".WithEnvironment(\"Organizations__Lifecycle__Enabled\", \"true\")",
            composition,
            StringComparison.Ordinal);
        Assert.Contains(
            ".WithEnvironment(\"Organizations__Retention__Enabled\", \"true\")",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Public_api_exposes_bounded_authentication_runtime_defaults()
    {
        using JsonDocument document = JsonDocument.Parse(
            RepositoryPaths.Read("src", "BunkFy.Host.Api", "appsettings.json"));
        JsonElement auth = document.RootElement.GetProperty("Auth");

        Assert.InRange(auth.GetProperty("MaximumActiveSessionsPerMember").GetInt32(), 1, 1000);
        Assert.InRange(auth.GetProperty("FailedLoginLimit").GetInt32(), 1, 100);
        Assert.InRange(auth.GetProperty("FailedLoginWindowMinutes").GetInt32(), 1, 1440);
    }

    [Fact]
    public void Bearer_api_hosts_require_active_auth_session_admission()
    {
        foreach (string host in new[] { "BunkFy.Host.Api", "BunkFy.Host.AdminApi" })
        {
            using JsonDocument document = JsonDocument.Parse(
                RepositoryPaths.Read("src", host, "appsettings.json"));
            Assert.Equal(
                "ActiveSession",
                document.RootElement
                    .GetProperty("Auth")
                    .GetProperty("BearerAdmission")
                    .GetProperty("Mode")
                    .GetString());
        }
    }

    [Fact]
    public void Long_running_auth_persistence_hosts_expose_bounded_failure_retention_defaults()
    {
        foreach (string host in AuthRetentionHosts)
        {
            using JsonDocument document = JsonDocument.Parse(
                RepositoryPaths.Read("src", host, "appsettings.json"));
            JsonElement retention = document.RootElement
                .GetProperty("Auth")
                .GetProperty("Retention");

            Assert.False(retention.GetProperty("Enabled").GetBoolean());
            Assert.InRange(retention.GetProperty("ExpiredExchangeHistoryHours").GetInt32(), 1, 8760);
            Assert.InRange(retention.GetProperty("PasswordRecoveryHistoryHours").GetInt32(), 1, 8760);
            Assert.InRange(retention.GetProperty("SessionHistoryDays").GetInt32(), 1, 3650);
            Assert.InRange(retention.GetProperty("AuthenticationChallengeHistoryHours").GetInt32(), 1, 8760);
            Assert.InRange(retention.GetProperty("ExpiredTotpEnrollmentHistoryHours").GetInt32(), 1, 8760);
            Assert.InRange(retention.GetProperty("DisabledTotpAuthenticatorHistoryDays").GetInt32(), 1, 3650);
            Assert.InRange(retention.GetProperty("MultiFactorFailureHistoryHours").GetInt32(), 1, 8760);
            Assert.InRange(retention.GetProperty("AuthenticationFailureHistoryHours").GetInt32(), 1, 8760);
        }

        string composition = RepositoryPaths.Read(
            "src",
            "Shared",
            "BunkFy.AppHost.Composition",
            "BunkFyBackendComposition.cs");
        Assert.Contains(
            ".WithEnvironment(\"Auth__Retention__Enabled\", \"true\")",
            composition,
            StringComparison.Ordinal);
    }

    private static void AssertFailClosedMaintenanceAdmission(JsonElement admission)
    {
        Assert.Equal("Pending", admission.GetProperty("ApprovalState").GetString());
        Assert.Equal("Unspecified", admission.GetProperty("MaintenanceOwner").GetString());
        Assert.Equal(0, admission.GetProperty("MaintenanceOwnerInstanceCount").GetInt32());
        Assert.False(admission.GetProperty("CurrentProcessOwnsMaintenance").GetBoolean());
        Assert.Equal(
            "Unspecified",
            admission.GetProperty("ExistingHistoryDisposition").GetString());
    }

    private static TimeSpan ParseDuration(JsonElement section, string propertyName) =>
        TimeSpan.Parse(section.GetProperty(propertyName).GetString()!, CultureInfo.InvariantCulture);
}
