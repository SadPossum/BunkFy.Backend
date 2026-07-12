namespace Integration.Tests;

using DotNet.Testcontainers.Containers;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Nats;
using Gma.Framework.Runtime;
using Integration.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Xunit;

public sealed class NatsEventBusIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Publish_succeeds_when_logger_throws_after_broker_ack()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();

        string streamName = $"GMA_LOGGER_{Guid.NewGuid():N}".ToUpperInvariant();
        await using NatsConnection connection = new(new NatsOpts
        {
            Url = AuthTestContainers.GetNatsConnectionString(nats),
        });
        NatsJetStreamEventBus firstBus = CreateBus(connection, streamName, new ThrowingLogger<NatsJetStreamEventBus>());

        await firstBus.PublishAsync(CreateMessage("tenant-logger", "logger-1"), CancellationToken.None);

        NatsJetStreamEventBus secondBus = CreateBus(connection, streamName, new ThrowingLogger<NatsJetStreamEventBus>());
        await secondBus.PublishAsync(CreateMessage("tenant-logger", "logger-2"), CancellationToken.None);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Publish_uses_outbox_message_id_for_jetstream_deduplication()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();

        string streamName = $"GMA_DEDUPE_{Guid.NewGuid():N}".ToUpperInvariant();
        await using NatsConnection connection = new(new NatsOpts
        {
            Url = AuthTestContainers.GetNatsConnectionString(nats),
        });
        NatsJetStreamEventBus eventBus = CreateBus(
            connection,
            streamName,
            NullLogger<NatsJetStreamEventBus>.Instance);
        OutboxMessageRecord message = CreateMessage("tenant-dedupe", "dedupe");

        await eventBus.PublishAsync(message, CancellationToken.None);
        await eventBus.PublishAsync(message, CancellationToken.None);

        NatsJSContext jetStream = new(connection);
        INatsJSConsumer consumer = await jetStream.CreateOrUpdateConsumerAsync(
            streamName,
            new ConsumerConfig($"dedupe-{Guid.NewGuid():N}")
            {
                FilterSubject = message.Subject,
                AckWait = TimeSpan.FromSeconds(5),
                MaxDeliver = 1,
                MaxAckPending = 10
            },
            CancellationToken.None);
        List<INatsJSMsg<string>> storedMessages = [];

        await foreach (INatsJSMsg<string> storedMessage in consumer.FetchAsync(
            new NatsJSFetchOpts { MaxMsgs = 10, Expires = TimeSpan.FromSeconds(2) },
            NatsDefaultSerializer<string>.Default,
            CancellationToken.None))
        {
            storedMessages.Add(storedMessage);
            await storedMessage.AckAsync(cancellationToken: CancellationToken.None);
        }

        Assert.Single(storedMessages);
        Assert.Equal(message.Payload, storedMessages[0].Data);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Managed_stream_applies_limits_and_external_mode_rejects_drift()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using NatsConnection connection = new(new NatsOpts
        {
            Url = AuthTestContainers.GetNatsConnectionString(nats),
        });
        string managedName = $"GMA_MANAGED_{Guid.NewGuid():N}".ToUpperInvariant();
        NatsJetStreamOptions managedOptions = new()
        {
            StreamName = managedName,
            MaxAge = TimeSpan.FromHours(12),
            MaxBytes = 16 * 1024 * 1024,
            MaxMessages = 25_000,
            DuplicateWindow = TimeSpan.FromMinutes(5),
        };
        using NatsJetStreamStreamManager managed = CreateStreamManager(connection, managedOptions);

        await managed.EnsureReadyAsync(CancellationToken.None);

        NatsJSContext jetStream = new(connection);
        INatsJSStream stream = await jetStream.GetStreamAsync(managedName, cancellationToken: CancellationToken.None);
        Assert.Equal(managedOptions.MaxAge, stream.Info.Config.MaxAge);
        Assert.Equal(managedOptions.MaxBytes, stream.Info.Config.MaxBytes);
        Assert.Equal(managedOptions.MaxMessages, stream.Info.Config.MaxMsgs);

        string externalName = $"GMA_EXTERNAL_{Guid.NewGuid():N}".ToUpperInvariant();
        await jetStream.CreateStreamAsync(
            new StreamConfig(externalName, [NatsJetStreamOptions.CreateSubjectWildcard("external-test")]),
            cancellationToken: CancellationToken.None);
        using NatsJetStreamStreamManager external = CreateStreamManager(
            connection,
            new NatsJetStreamOptions
            {
                StreamName = externalName,
                ManagementMode = NatsStreamManagementMode.External,
            },
            "external-test");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => external.EnsureReadyAsync(CancellationToken.None));
        Assert.Contains("does not match configured safety limits", exception.Message, StringComparison.Ordinal);
    }

    private static NatsJetStreamEventBus CreateBus(
        INatsConnection connection,
        string streamName,
        ILogger<NatsJetStreamEventBus> logger)
    {
        NatsJetStreamStreamManager manager = CreateStreamManager(
            connection,
            new NatsJetStreamOptions { StreamName = streamName });
        return new NatsJetStreamEventBus(connection, manager, logger);
    }

    private static NatsJetStreamStreamManager CreateStreamManager(
        INatsConnection connection,
        NatsJetStreamOptions options,
        string applicationNamespace = "gma") =>
        new(
            connection,
            Options.Create(options),
            Options.Create(new ApplicationIdentityOptions { Namespace = applicationNamespace }),
            NullLogger<NatsJetStreamStreamManager>.Instance);

    private static OutboxMessageRecord CreateMessage(string scopeId, string suffix) => new(
        Guid.NewGuid(),
        "gma.test.logger.v1",
        "Integration.Tests.LoggerEvent",
        1,
        scopeId,
        DateTimeOffset.UtcNow,
        $$"""{"suffix":"{{suffix}}"}""");

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Logger unavailable.");
    }
}
