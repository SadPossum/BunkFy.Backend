namespace Integration.Tests;

using BunkFy.Host.Worker;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class OutboxPublisherIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Member_registered_event_is_published_and_marked_processed()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_auth_tests")
            .Build();
        await nats.StartAsync();
        await postgreSql.StartAsync();

        await using AuthTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            AuthTestContainers.GetNatsConnectionString(nats),
            disableOutboxPublisher: false);

        await application.MigrateDatabaseAsync();
        using HttpClient client = application.CreateClient();

        await AuthApiClient.RegisterAsync(client, "tenant-events", "events@example.com");

        int processedAfterPublish = await application.WaitForProcessedOutboxMessagesAsync(1, TimeSpan.FromSeconds(20));
        int pendingAfterPublish = await application.CountPendingOutboxMessagesAsync();

        Assert.Equal(1, processedAfterPublish);
        Assert.Equal(0, pendingAfterPublish);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Worker_drains_auth_outbox_when_api_publishing_is_disabled()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_auth_worker_tests")
            .Build();
        await nats.StartAsync();
        await postgreSql.StartAsync();

        string natsConnectionString = AuthTestContainers.GetNatsConnectionString(nats);
        await using AuthTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            natsConnectionString,
            disableOutboxPublisher: true);
        using IHost worker = BuildAuthWorker(
            postgreSql.GetConnectionString(),
            natsConnectionString,
            $"GMA_WORKER_{Guid.NewGuid():N}".ToUpperInvariant());

        await application.MigrateDatabaseAsync();
        using HttpClient client = application.CreateClient();

        await AuthApiClient.RegisterAsync(client, "tenant-worker", "worker@example.com");
        Assert.Equal(1, await application.CountPendingOutboxMessagesAsync().ConfigureAwait(false));

        using (IServiceScope workerScope = worker.Services.CreateScope())
        {
            Assert.Contains(
                workerScope.ServiceProvider.GetServices<IOutboxStore>(),
                store => store.ModuleName == "auth");
        }

        await worker.StartAsync().ConfigureAwait(false);
        try
        {
            int processedAfterWorkerPublish =
                await application.WaitForProcessedOutboxMessagesAsync(1, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            int pendingAfterWorkerPublish = await application.CountPendingOutboxMessagesAsync().ConfigureAwait(false);

            if (processedAfterWorkerPublish != 1)
            {
                OutboxSnapshot snapshot = await application.GetOnlyOutboxSnapshotAsync().ConfigureAwait(false);
                Assert.Fail(
                    $"Auth Worker did not process outbox message {snapshot.Id:D}. " +
                    $"Attempts={snapshot.Attempts}; LockedBy={snapshot.LockedBy ?? "none"}; " +
                    $"LockedUntilUtc={snapshot.LockedUntilUtc?.ToString("O") ?? "none"}; " +
                    $"NextAttemptAtUtc={snapshot.NextAttemptAtUtc?.ToString("O") ?? "none"}; " +
                    $"Error={snapshot.Error ?? "none"}.");
            }

            Assert.Equal(0, pendingAfterWorkerPublish);
        }
        finally
        {
            await worker.StopAsync().ConfigureAwait(false);
        }
    }

    private static IHost BuildAuthWorker(
        string postgreSqlConnectionString,
        string natsConnectionString,
        string streamName)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Integration",
        });
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ApplicationIdentity:Namespace"] = "bunkfy";
        builder.Configuration["ConnectionStrings:PostgreSql"] = postgreSqlConnectionString;
        builder.Configuration["ConnectionStrings:nats"] = natsConnectionString;
        builder.Configuration["Tenancy:Enabled"] = "true";
        builder.Configuration["NatsJetStream:Enabled"] = "true";
        builder.Configuration["NatsJetStream:StreamName"] = streamName;
        builder.Configuration["Outbox:BatchSize"] = "5";
        builder.Configuration["Outbox:PollIntervalMilliseconds"] = "100";
        builder.Configuration["Outbox:LockDurationMilliseconds"] = "1000";
        builder.Configuration["Worker:Modules:Auth"] = "true";
        AuthTestConfiguration.ConfigureTokenHashing(builder.Configuration);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.AddWorkerHost();
        builder.ValidateModuleComposition();

        return builder.Build();
    }
}
