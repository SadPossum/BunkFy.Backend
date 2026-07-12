namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Runs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StartAdapterRunCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Retired_property_rejects_run_before_creating_ingestion_state()
    {
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged,
            "configuration://main",
            null,
            Now).Value;
        FakeRunRepository runs = new();
        StartAdapterRunCommandHandler handler = new(
            new FakeConnectionRepository(connection),
            new FakePropertyProjection(active: false),
            runs,
            new TestDescriptors(),
            new TestScope(),
            new TestIds(),
            new TestClock());

        var result = await handler.HandleAsync(
            new StartAdapterRunCommand(connection.Id, Guid.NewGuid(), 1),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.PropertyNotActive, result.Error);
        Assert.Empty(runs.Items);
    }

    [Fact]
    public async Task Push_connection_cannot_start_a_task_linked_run()
    {
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", Guid.NewGuid(), "fake.http", AdapterExecutionMode.Push,
            IngestionConflictPolicy.SuggestionsOnly, "configuration://main", null, Now).Value;
        FakeRunRepository runs = new();
        StartAdapterRunCommandHandler handler = new(
            new FakeConnectionRepository(connection), new FakePropertyProjection(active: true), runs,
            new TestDescriptors(), new TestScope(), new TestIds(), new TestClock());

        var result = await handler.HandleAsync(
            new StartAdapterRunCommand(connection.Id, Guid.NewGuid(), 1), CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.AdapterExecutionModeNotTaskRunnable, result.Error);
        Assert.Empty(runs.Items);
    }

    [Fact]
    public async Task A_second_task_cannot_overlap_an_active_connection_run()
    {
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", Guid.NewGuid(), "fake.http", AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly, "configuration://main", null, Now).Value;
        FakeRunRepository runs = new();
        runs.Items.Add(IngestionRun.Start(
            Guid.NewGuid(), "tenant-a", connection.Id, connection.PropertyId,
            Guid.NewGuid(), 1, null, Now).Value);
        StartAdapterRunCommandHandler handler = new(
            new FakeConnectionRepository(connection), new FakePropertyProjection(active: true), runs,
            new TestDescriptors(), new TestScope(), new TestIds(), new TestClock());

        var result = await handler.HandleAsync(
            new StartAdapterRunCommand(connection.Id, Guid.NewGuid(), 1), CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.ConnectionRunAlreadyActive, result.Error);
        Assert.Single(runs.Items);
    }

    private sealed class FakeConnectionRepository(AdapterConnection connection) : IAdapterConnectionRepository
    {
        public Task<AdapterConnection?> GetAsync(Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<AdapterConnection?>(connectionId == connection.Id ? connection : null);

        public Task<AdapterConnection?> GetAsync(Guid propertyId, Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<AdapterConnection?>(propertyId == connection.PropertyId && connectionId == connection.Id ? connection : null);

        public Task AddAsync(AdapterConnection added, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakePropertyProjection(bool active) : IIngestionPropertyProjectionRepository
    {
        public Task ApplyAsync(IngestionPropertyProjectionWriteModel property, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken) =>
            Task.FromResult(active);
    }

    private sealed class FakeRunRepository : IIngestionRunRepository
    {
        public List<IngestionRun> Items { get; } = [];

        public Task<IngestionRun?> GetAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(item => item.Id == runId));

        public Task<IngestionRun?> FindByTaskExecutionAsync(
            Guid taskRunId,
            int taskAttempt,
            CancellationToken cancellationToken) => Task.FromResult(this.Items.SingleOrDefault(
                item => item.TaskRunId == taskRunId && item.TaskAttempt == taskAttempt));

        public Task<IngestionRun?> FindActiveByConnectionAsync(
            Guid connectionId,
            CancellationToken cancellationToken) => Task.FromResult(this.Items.SingleOrDefault(
                item => item.ConnectionId == connectionId && item.State == IngestionRunState.Running));

        public Task AddAsync(IngestionRun run, CancellationToken cancellationToken)
        {
            this.Items.Add(run);
            return Task.CompletedTask;
        }
    }

    private sealed class TestScope : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestIds : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestDescriptors : IAdapterDescriptorRegistry
    {
        private static readonly AdapterDescriptor Descriptor = new(
            "fake.http", 1, 1, [AdapterExecutionMode.Polling, AdapterExecutionMode.Push]);

        public IReadOnlyCollection<AdapterDescriptor> GetAll() => [Descriptor];

        public bool TryGet(string adapterType, out AdapterDescriptor? descriptor)
        {
            descriptor = adapterType == Descriptor.AdapterType ? Descriptor : null;
            return descriptor is not null;
        }
    }
}
