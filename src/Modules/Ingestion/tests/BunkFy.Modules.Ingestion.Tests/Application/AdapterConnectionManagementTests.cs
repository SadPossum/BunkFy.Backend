namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.DataGovernance;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Runs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterConnectionManagementTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_requires_an_active_local_property_projection()
    {
        FakeConnectionRepository connections = new();
        CreateAdapterConnectionCommand command = new(
            Guid.NewGuid(),
            "fake.http",
            AdapterExecutionMode.Polling,
            AdapterConflictPolicy.SuggestionsOnly,
            "configuration://main",
            null);

        var rejected = await new CreateAdapterConnectionCommandHandler(
            connections, new TestCountryPolicyAdmission(allowed: false), new TestDescriptors(),
            new TestScope(), new TestIds(), new TestClock())
            .HandleAsync(command, CancellationToken.None);
        var created = await new CreateAdapterConnectionCommandHandler(
            connections, new TestCountryPolicyAdmission(), new TestDescriptors(),
            new TestScope(), new TestIds(), new TestClock())
            .HandleAsync(command, CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.CountryPolicyDenied(CountryPolicyDecisionReason.MissingBinding),
            rejected.Error);
        Assert.True(created.IsSuccess, created.Error.Code);
        Assert.Equal(AdapterConnectionStatus.Enabled, created.Value.Status);
        Assert.Equal("fake.http", created.Value.AdapterType);
        Assert.Single(connections.Items);
    }

    [Fact]
    public async Task Update_and_disable_use_property_scope_and_expected_version()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http", AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly, "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);

        var updated = await new UpdateAdapterConnectionCommandHandler(
            connections, new TestDescriptors(), new TestClock()).HandleAsync(
            new(propertyId, connection.Id, AdapterExecutionMode.Continuous,
                AdapterConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged,
                "configuration://secondary", SecretReferenceUpdateMode.Replace, "secret://secondary", 1),
            CancellationToken.None);
        var wrongProperty = await new SetAdapterConnectionEnabledCommandHandler(
            connections, new EmptyRunRepository(), new TestCountryPolicyAdmission(allowed: false), new TestClock()).HandleAsync(
            new(Guid.NewGuid(), connection.Id, Enabled: false, ExpectedVersion: 2), CancellationToken.None);
        var disabled = await new SetAdapterConnectionEnabledCommandHandler(
            connections, new EmptyRunRepository(), new TestCountryPolicyAdmission(allowed: false), new TestClock()).HandleAsync(
            new(propertyId, connection.Id, Enabled: false, ExpectedVersion: 2), CancellationToken.None);
        var deniedEnable = await new SetAdapterConnectionEnabledCommandHandler(
            connections, new EmptyRunRepository(), new TestCountryPolicyAdmission(allowed: false), new TestClock()).HandleAsync(
            new(propertyId, connection.Id, Enabled: true, ExpectedVersion: 3), CancellationToken.None);

        Assert.True(updated.IsSuccess, updated.Error.Code);
        Assert.Equal(AdapterExecutionMode.Continuous, updated.Value.ExecutionMode);
        Assert.True(updated.Value.HasSecretReference);
        Assert.Equal(IngestionApplicationErrors.ConnectionNotFound, wrongProperty.Error);
        Assert.Equal(AdapterConnectionStatus.Disabled, disabled.Value.Status);
        Assert.Equal(3, disabled.Value.Version);
        Assert.Equal(
            IngestionApplicationErrors.CountryPolicyDenied(CountryPolicyDecisionReason.MissingBinding),
            deniedEnable.Error);
        Assert.Equal(AdapterConnectionState.Disabled, connection.State);
        Assert.Equal(3, connection.Version);
    }

    [Fact]
    public async Task Secret_reference_updates_are_explicit_and_reads_only_expose_presence()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http", AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly, "configuration://main", "secret://initial", Now).Value;
        FakeConnectionRepository connections = new(connection);
        UpdateAdapterConnectionCommandHandler handler = new(connections, new TestDescriptors(), new TestClock());

        var kept = await handler.HandleAsync(new(
            propertyId, connection.Id, AdapterExecutionMode.Continuous, AdapterConflictPolicy.SuggestionsOnly,
            "configuration://changed", SecretReferenceUpdateMode.Keep, null, 1), CancellationToken.None);
        Assert.True(kept.IsSuccess, kept.Error.Code);
        Assert.True(kept.Value.HasSecretReference);
        Assert.Equal("secret://initial", connection.SecretReference);

        var invalid = await handler.HandleAsync(new(
            propertyId, connection.Id, AdapterExecutionMode.Continuous, AdapterConflictPolicy.SuggestionsOnly,
            "configuration://changed", SecretReferenceUpdateMode.Clear, "secret://unexpected", 2),
            CancellationToken.None);
        Assert.Equal(IngestionApplicationErrors.SecretReferenceUpdateInvalid, invalid.Error);
        Assert.Equal(2, connection.Version);

        var cleared = await handler.HandleAsync(new(
            propertyId, connection.Id, AdapterExecutionMode.Continuous, AdapterConflictPolicy.SuggestionsOnly,
            "configuration://changed", SecretReferenceUpdateMode.Clear, null, 2), CancellationToken.None);

        Assert.True(cleared.IsSuccess, cleared.Error.Code);
        Assert.False(cleared.Value.HasSecretReference);
        Assert.Null(connection.SecretReference);
    }

    [Fact]
    public async Task Connection_configuration_requires_a_registered_compatible_adapter_capability()
    {
        Guid propertyId = Guid.NewGuid();
        FakeConnectionRepository connections = new();
        CreateAdapterConnectionCommandHandler handler = new(
            connections, new TestCountryPolicyAdmission(), new TestDescriptors(),
            new TestScope(), new TestIds(), new TestClock());

        var unknown = await handler.HandleAsync(new(
            propertyId, "missing.adapter", AdapterExecutionMode.Polling, AdapterConflictPolicy.SuggestionsOnly,
            "configuration://main", null), CancellationToken.None);
        var unsupported = await handler.HandleAsync(new(
            propertyId, "fake.http", AdapterExecutionMode.Push, AdapterConflictPolicy.SuggestionsOnly,
            "configuration://main", null), CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.AdapterTypeNotRegistered, unknown.Error);
        Assert.Equal(IngestionApplicationErrors.AdapterExecutionModeUnsupported, unsupported.Error);
        Assert.Empty(connections.Items);
    }

    [Fact]
    public async Task Polling_schedule_honors_provider_minimum_and_can_be_cleared_optimistically()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http", AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly, "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);
        TestDescriptors descriptors = new();

        var tooFrequent = await new ConfigureAdapterConnectionPollingScheduleCommandHandler(
            connections, descriptors, new TestClock()).HandleAsync(
            new(propertyId, connection.Id, IntervalSeconds: 60, MaxAttempts: 3, ExpectedVersion: 1),
            CancellationToken.None);
        var configured = await new ConfigureAdapterConnectionPollingScheduleCommandHandler(
            connections, descriptors, new TestClock()).HandleAsync(
            new(propertyId, connection.Id, IntervalSeconds: 180, MaxAttempts: 4, ExpectedVersion: 1),
            CancellationToken.None);
        var cleared = await new ClearAdapterConnectionPollingScheduleCommandHandler(
            connections, new TestClock()).HandleAsync(
            new(propertyId, connection.Id, configured.Value.Version), CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.PollingIntervalBelowAdapterMinimum, tooFrequent.Error);
        Assert.Equal(180, configured.Value.PollingIntervalSeconds);
        Assert.Equal(4, configured.Value.PollingScheduleMaxAttempts);
        Assert.NotNull(configured.Value.PollingScheduleConfiguredAtUtc);
        Assert.Null(cleared.Value.PollingIntervalSeconds);
        Assert.Equal(3, cleared.Value.Version);
    }

    private sealed class FakeConnectionRepository(params AdapterConnection[] items) : IAdapterConnectionRepository
    {
        public List<AdapterConnection> Items { get; } = [.. items];

        public Task<AdapterConnection?> GetAsync(Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(item => item.Id == connectionId));

        public Task<AdapterConnection?> GetAsync(
            Guid propertyId,
            Guid connectionId,
            CancellationToken cancellationToken) => Task.FromResult(this.Items.SingleOrDefault(
            item => item.Id == connectionId && item.PropertyId == propertyId));

        public Task AddAsync(AdapterConnection connection, CancellationToken cancellationToken)
        {
            this.Items.Add(connection);
            return Task.CompletedTask;
        }
    }

    private sealed class TestScope : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIds : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private sealed class EmptyRunRepository : IIngestionRunRepository
    {
        public Task<IngestionRun?> GetAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult<IngestionRun?>(null);

        public Task<IngestionRun?> FindByTaskExecutionAsync(
            Guid taskRunId, int taskAttempt, CancellationToken cancellationToken) =>
            Task.FromResult<IngestionRun?>(null);

        public Task<IngestionRun?> FindActiveByConnectionAsync(
            Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<IngestionRun?>(null);

        public Task AddAsync(IngestionRun run, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestDescriptors : IAdapterDescriptorRegistry
    {
        private static readonly AdapterDescriptor Descriptor = new(
            "fake.http", 1, 1, [AdapterExecutionMode.Polling, AdapterExecutionMode.Continuous],
            new AdapterPollingCapability(TimeSpan.FromSeconds(120), TimeSpan.FromMinutes(5)));

        public IReadOnlyCollection<AdapterDescriptor> GetAll() => [Descriptor];

        public bool TryGet(string adapterType, out AdapterDescriptor? descriptor)
        {
            descriptor = string.Equals(adapterType, Descriptor.AdapterType, StringComparison.Ordinal)
                ? Descriptor
                : null;
            return descriptor is not null;
        }
    }
}
