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
            Assert.True(jetStream.GetProperty("Replicas").GetInt32() > 0);

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
            Assert.InRange(retention.GetProperty("AuthenticationFailureHistoryHours").GetInt32(), 1, 8760);
        }
    }

    private static TimeSpan ParseDuration(JsonElement section, string propertyName) =>
        TimeSpan.Parse(section.GetProperty(propertyName).GetString()!, CultureInfo.InvariantCulture);
}
