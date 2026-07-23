namespace BunkFy.Modules.Ingestion.Tests.Application;

using System.Text;
using BunkFy.DataGovernance;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Runs;
using BunkFy.Modules.Ingestion.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReceiveObservationCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accepted_observation_stores_raw_payload_and_receipt()
    {
        TestContext context = CreateContext();
        ReceiveObservationCommand command = CreateCommand(context.Connection.Id);

        Result<AdapterObservationResult> result = await context.Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdapterObservationDisposition.Accepted, result.Value.Disposition);
        Assert.Single(context.Receipts.Items);
        ObservationCountryPolicyEvidence evidence = Assert.IsType<ObservationCountryPolicyEvidence>(
            context.Receipts.Items[0].CountryPolicyEvidence);
        Assert.Equal("GB", evidence.OperatingCountryCode);
        Assert.Equal("gb-hostel", evidence.PolicyId);
        Assert.Equal("reservation-ingestion", evidence.PurposeCode);
        Assert.Equal("adapter-ingress", evidence.ProcessingSurface);
        Assert.Equal("approved-adapter", evidence.SourceProvenance);
        Assert.Equal(Now, evidence.EvaluatedAtUtc);
        Assert.Single(context.RawPayloads.Writes);
        Assert.Equal(result.Value.ReceiptId, context.RawPayloads.Writes[0].PayloadId);
        Assert.IsType<ObservationReceiptAcceptedIntegrationEvent>(Assert.Single(context.Outbox.Events));
    }

    [Fact]
    public async Task Same_source_revision_is_duplicate_without_second_payload_write()
    {
        TestContext context = CreateContext();
        ReceiveObservationCommand first = CreateCommand(context.Connection.Id);
        ReceiveObservationCommand retry = first with { OperationId = Guid.NewGuid() };

        Result<AdapterObservationResult> accepted = await context.Handler.HandleAsync(first, CancellationToken.None);
        Result<AdapterObservationResult> duplicate = await context.Handler.HandleAsync(retry, CancellationToken.None);

        Assert.True(duplicate.IsSuccess);
        Assert.Equal(AdapterObservationDisposition.Duplicate, duplicate.Value.Disposition);
        Assert.Equal(accepted.Value.ReceiptId, duplicate.Value.ReceiptId);
        Assert.Single(context.RawPayloads.Writes);
        Assert.Single(context.Receipts.Items);
        Assert.Single(context.Outbox.Events);
    }

    [Fact]
    public async Task Reused_operation_id_with_different_content_is_rejected()
    {
        TestContext context = CreateContext();
        ReceiveObservationCommand first = CreateCommand(context.Connection.Id);
        byte[] changedPayload = Encoding.UTF8.GetBytes("{\"revision\":2}");
        ReceiveObservationCommand conflict = first with
        {
            Payload = changedPayload,
            ContentSha256 = AdapterPayloadHash.ComputeSha256(changedPayload)
        };

        await context.Handler.HandleAsync(first, CancellationToken.None);
        Result<AdapterObservationResult> result = await context.Handler.HandleAsync(conflict, CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.OperationIdentityConflict, result.Error);
        Assert.Single(context.RawPayloads.Writes);
    }

    [Fact]
    public async Task Observation_cannot_use_another_connections_run()
    {
        IngestionRun run = IngestionRun.Start(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            null,
            Now).Value;
        TestContext context = CreateContext(run);
        ReceiveObservationCommand command = CreateCommand(context.Connection.Id) with { RunId = run.Id };

        Result<AdapterObservationResult> result = await context.Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.RunConnectionMismatch, result.Error);
        Assert.Empty(context.RawPayloads.Writes);
    }

    [Fact]
    public async Task Payload_hash_is_verified_again_at_the_application_boundary()
    {
        TestContext context = CreateContext();
        ReceiveObservationCommand command = CreateCommand(context.Connection.Id) with
        {
            ContentSha256 = new string('0', AdapterProtocolLimits.Sha256Length)
        };

        Result<AdapterObservationResult> result = await context.Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.PayloadHashMismatch, result.Error);
        Assert.Empty(context.RawPayloads.Writes);
    }

    [Fact]
    public async Task Retired_property_rejects_observation_before_writing_ingestion_state()
    {
        TestContext context = CreateContext(propertyActive: false);

        Result<AdapterObservationResult> result = await context.Handler.HandleAsync(
            CreateCommand(context.Connection.Id),
            CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.CountryPolicyDenied(CountryPolicyDecisionReason.MissingBinding),
            result.Error);
        Assert.Empty(context.Receipts.Items);
        Assert.Empty(context.RawPayloads.Writes);
        Assert.Empty(context.Outbox.Events);
    }

    private static TestContext CreateContext(IngestionRun? run = null, bool propertyActive = true)
    {
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged,
            "config/fake-http",
            null,
            Now).Value;
        FakeReceiptRepository receipts = new();
        FakeRawPayloadStore rawPayloads = new();
        RecordingOutbox outbox = new();
        ServiceCollection services = new();
        services.AddSingleton<IAdapterConnectionRepository>(new FakeConnectionRepository(connection));
        services.AddSingleton<IIngestionCountryPolicyAdmission>(
            new TestCountryPolicyAdmission(allowed: propertyActive));
        services.AddSingleton<IIngestionRunRepository>(new FakeRunRepository(run));
        services.AddSingleton<IObservationReceiptRepository>(receipts);
        services.AddSingleton<IObservationReprocessingAttemptRepository>(new FakeReprocessingAttemptRepository());
        services.AddSingleton<IRawPayloadStore>(rawPayloads);
        services.AddSingleton<IIngestionRetentionPolicy>(new TestRetentionPolicy());
        services.AddSingleton<IOutboxWriterRegistry>(new RecordingOutboxRegistry(outbox));
        services.AddSingleton<IScopeContext>(new TestScopeContext());
        services.AddSingleton<ISystemClock>(new TestClock());
        services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        services.AddIngestionApplication();
        ServiceProvider provider = services.BuildServiceProvider();
        return new(
            connection,
            receipts,
            rawPayloads,
            outbox,
            provider.GetRequiredService<ICommandHandler<ReceiveObservationCommand, AdapterObservationResult>>());
    }

    private static ReceiveObservationCommand CreateCommand(Guid connectionId)
    {
        byte[] payload = Encoding.UTF8.GetBytes("{\"revision\":1}");
        return new(
            connectionId,
            RunId: null,
            Guid.NewGuid(),
            "reservation.changed",
            "booking-123",
            "1",
            Now.AddMinutes(-2),
            Now.AddMinutes(-1),
            "application/json",
            payload,
            AdapterPayloadHash.ComputeSha256(payload));
    }

    private sealed record TestContext(
        AdapterConnection Connection,
        FakeReceiptRepository Receipts,
        FakeRawPayloadStore RawPayloads,
        RecordingOutbox Outbox,
        ICommandHandler<ReceiveObservationCommand, AdapterObservationResult> Handler);

    private sealed class FakeConnectionRepository(AdapterConnection connection) : IAdapterConnectionRepository
    {
        public Task<AdapterConnection?> GetAsync(Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<AdapterConnection?>(connectionId == connection.Id ? connection : null);

        public Task<AdapterConnection?> GetAsync(Guid propertyId, Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<AdapterConnection?>(propertyId == connection.PropertyId && connectionId == connection.Id ? connection : null);

        public Task AddAsync(AdapterConnection added, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRunRepository(IngestionRun? run) : IIngestionRunRepository
    {
        public Task<IngestionRun?> GetAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(run?.Id == runId ? run : null);

        public Task<IngestionRun?> FindByTaskExecutionAsync(
            Guid taskRunId,
            int taskAttempt,
            CancellationToken cancellationToken) =>
            Task.FromResult(run?.TaskRunId == taskRunId && run.TaskAttempt == taskAttempt ? run : null);

        public Task<IngestionRun?> FindActiveByConnectionAsync(
            Guid connectionId,
            CancellationToken cancellationToken) => Task.FromResult(
                run is not null && run.ConnectionId == connectionId && run.State == IngestionRunState.Running
                    ? run
                    : null);

        public Task AddAsync(IngestionRun added, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeReceiptRepository : IObservationReceiptRepository
    {
        public List<ObservationReceipt> Items { get; } = [];

        public Task<ObservationReceipt?> GetAsync(Guid receiptId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.FirstOrDefault(receipt => receipt.Id == receiptId));

        public Task<ObservationReceipt?> FindByOperationAsync(
            Guid connectionId,
            Guid operationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.FirstOrDefault(receipt =>
                receipt.ConnectionId == connectionId && receipt.OperationId == operationId));

        public Task<ObservationReceipt?> FindByDeduplicationKeyAsync(
            Guid connectionId,
            string deduplicationKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.FirstOrDefault(receipt =>
                receipt.ConnectionId == connectionId && receipt.DeduplicationKey == deduplicationKey));

        public Task AddAsync(ObservationReceipt receipt, CancellationToken cancellationToken)
        {
            this.Items.Add(receipt);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReprocessingAttemptRepository : IObservationReprocessingAttemptRepository
    {
        public Task<BunkFy.Modules.Ingestion.Domain.Reprocessing.ObservationReprocessingAttempt?> GetAsync(
            Guid attemptId, CancellationToken cancellationToken) => Task.FromResult<
                BunkFy.Modules.Ingestion.Domain.Reprocessing.ObservationReprocessingAttempt?>(null);

        public Task<BunkFy.Modules.Ingestion.Domain.Reprocessing.ObservationReprocessingAttempt?> FindActiveBySourceAsync(
            Guid sourceReceiptId, CancellationToken cancellationToken) => Task.FromResult<
                BunkFy.Modules.Ingestion.Domain.Reprocessing.ObservationReprocessingAttempt?>(null);

        public Task AddAsync(
            BunkFy.Modules.Ingestion.Domain.Reprocessing.ObservationReprocessingAttempt attempt,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeRawPayloadStore : IRawPayloadStore
    {
        public List<RawPayloadWrite> Writes { get; } = [];

        public Task StoreAsync(RawPayloadWrite write, CancellationToken cancellationToken)
        {
            this.Writes.Add(write);
            return Task.CompletedTask;
        }

        public Task<RawPayloadRead?> ReadAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestRetentionPolicy : IIngestionRetentionPolicy
    {
        public DateTimeOffset GetRawPayloadRetainUntilUtc(
            Guid propertyId,
            Guid connectionId,
            DateTimeOffset receivedAtUtc) => receivedAtUtc.AddDays(30);

        public DateTimeOffset GetSensitiveHistoryRetainUntilUtc(
            Guid propertyId,
            Guid connectionId,
            DateTimeOffset terminalAtUtc) => terminalAtUtc.AddDays(90);
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => IngestionModuleMetadata.Name;
        public List<IIntegrationEvent> Events { get; } = [];

        public Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRegistry(RecordingOutbox outbox) : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName) => outbox;
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
