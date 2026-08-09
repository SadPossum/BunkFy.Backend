namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.DataGovernance;
using BunkFy.Adapter.Abstractions;
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
    public async Task Create_requires_a_non_empty_operation_id_without_binding_it()
    {
        FakeConnectionRepository connections = new();
        FakeConnectionManagementOperationRepository operations = new();
        CreateAdapterConnectionCommandHandler handler = new(
            connections,
            operations,
            TestIngestionExecution.Create(connections),
            new TestCountryPolicyAdmission(),
            new TestDescriptors(),
            new TestScope(),
            new TestClock());

        var result = await handler.HandleAsync(
            new(
                Guid.Empty,
                Guid.NewGuid(),
                "fake.http",
                AdapterExecutionMode.Polling,
                AdapterConflictPolicy.SuggestionsOnly,
                "configuration://main",
                null),
            CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationInvalid,
            result.Error);
        Assert.Empty(connections.Items);
        Assert.Empty(operations.Items);
    }

    [Fact]
    public async Task Create_requires_an_active_local_property_projection()
    {
        FakeConnectionRepository connections = new();
        FakeConnectionManagementOperationRepository operations = new();
        CreateAdapterConnectionCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "fake.http",
            AdapterExecutionMode.Polling,
            AdapterConflictPolicy.SuggestionsOnly,
            "configuration://main",
            null);

        var rejected = await new CreateAdapterConnectionCommandHandler(
            connections, operations, TestIngestionExecution.Create(connections),
            new TestCountryPolicyAdmission(allowed: false), new TestDescriptors(),
            new TestScope(), new TestClock())
            .HandleAsync(command, CancellationToken.None);
        var created = await new CreateAdapterConnectionCommandHandler(
            connections, operations, TestIngestionExecution.Create(connections),
            new TestCountryPolicyAdmission(), new TestDescriptors(),
            new TestScope(), new TestClock())
            .HandleAsync(command, CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.CountryPolicyDenied(CountryPolicyDecisionReason.MissingBinding),
            rejected.Error);
        Assert.True(created.IsSuccess, created.Error.Code);
        Assert.Equal(AdapterConnectionStatus.Enabled, created.Value.Status);
        AdapterConnection stored = Assert.Single(connections.Items);
        Assert.Equal(command.OperationId, stored.Id);
        Assert.Equal(created.Value.ConnectionId, stored.Id);
        Assert.Equal("fake.http", stored.AdapterType);
        Assert.Single(operations.Items);
    }

    [Fact]
    public async Task Create_is_denied_by_tenant_lifecycle_before_domain_state_is_added()
    {
        FakeConnectionRepository connections = new();
        FakeConnectionManagementOperationRepository operations = new();
        CreateAdapterConnectionCommandHandler handler = new(
            connections,
            operations,
            TestIngestionExecution.Create(connections),
            new TestCountryPolicyAdmission(),
            new TestDescriptors(),
            new TestScope(),
            new TestClock(),
            [new TestLifecyclePolicy(
                IngestionTenantLifecycleDecision.Restricted)]);

        var result = await handler.HandleAsync(
            new CreateAdapterConnectionCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "fake.http",
                AdapterExecutionMode.Polling,
                AdapterConflictPolicy.SuggestionsOnly,
                "configuration://main",
                null),
            CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.TenantLifecycleRestricted,
            result.Error);
        Assert.Empty(connections.Items);
        Assert.Empty(operations.Items);
    }

    [Fact]
    public async Task Create_exact_retry_replays_and_changed_intent_conflicts()
    {
        Guid operationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        FakeConnectionRepository connections = new();
        FakeConnectionManagementOperationRepository operations = new();
        CreateAdapterConnectionCommandHandler handler = new(
            connections,
            operations,
            TestIngestionExecution.Create(connections),
            new TestCountryPolicyAdmission(),
            new TestDescriptors(),
            new TestScope(),
            new TestClock());
        CreateAdapterConnectionCommand command = new(
            operationId,
            propertyId,
            " FAKE.HTTP ",
            AdapterExecutionMode.Polling,
            AdapterConflictPolicy.SuggestionsOnly,
            " configuration://main ",
            " secret://main ");

        var created = await handler.HandleAsync(command, CancellationToken.None);
        var replayed = await handler.HandleAsync(
            command with
            {
                AdapterType = "fake.http",
                ConfigurationReference = "configuration://main",
                SecretReference = "secret://main"
            },
            CancellationToken.None);
        AdapterConnection connection = Assert.Single(connections.Items);
        Assert.True(connection.Configure(
            AdapterExecutionMode.Continuous,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://updated-later",
            "secret://updated-later",
            connection.Version,
            Now.AddMinutes(1)).IsSuccess);
        var replayedAfterUpdate = await handler.HandleAsync(
            command,
            CancellationToken.None);
        var conflicted = await handler.HandleAsync(
            command with { ConfigurationReference = "configuration://changed" },
            CancellationToken.None);

        Assert.True(created.IsSuccess, created.Error.Code);
        Assert.True(replayed.IsSuccess, replayed.Error.Code);
        Assert.Equal(created.Value, replayed.Value);
        Assert.True(
            replayedAfterUpdate.IsSuccess,
            replayedAfterUpdate.Error.Code);
        Assert.Equal(2, replayedAfterUpdate.Value.Version);
        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            conflicted.Error);
        Assert.Single(connections.Items);
        IngestionConnectionManagementOperationRecord operation =
            Assert.Single(operations.Items);
        Assert.Equal(operationId, operation.OperationId);
        Assert.Equal(operationId, operation.ConnectionId);
        Assert.Equal(64, operation.RequestFingerprint.Length);
    }

    [Fact]
    public async Task Create_rejects_a_legacy_connection_without_a_matching_receipt()
    {
        Guid operationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        AdapterConnection legacy = AdapterConnection.Create(
            operationId,
            "tenant-a",
            propertyId,
            "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://legacy",
            null,
            Now).Value;
        FakeConnectionRepository connections = new(legacy);
        FakeConnectionManagementOperationRepository operations = new();
        CreateAdapterConnectionCommandHandler handler = new(
            connections,
            operations,
            TestIngestionExecution.Create(connections),
            new TestCountryPolicyAdmission(),
            new TestDescriptors(),
            new TestScope(),
            new TestClock());

        var result = await handler.HandleAsync(
            new(
                operationId,
                propertyId,
                "fake.http",
                AdapterExecutionMode.Polling,
                AdapterConflictPolicy.SuggestionsOnly,
                "configuration://legacy",
                null),
            CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            result.Error);
        Assert.Single(connections.Items);
        Assert.Empty(operations.Items);
    }

    [Fact]
    public async Task Update_requires_a_non_empty_operation_id_without_binding_it()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();

        var result = await CreateUpdateHandler(connections, operations)
            .HandleAsync(
                new(
                    Guid.Empty,
                    propertyId,
                    connection.Id,
                    AdapterExecutionMode.Continuous,
                    AdapterConflictPolicy.SuggestionsOnly,
                    "configuration://updated",
                    SecretReferenceUpdateMode.Keep,
                    null,
                    connection.Version),
                CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationInvalid,
            result.Error);
        Assert.Equal(1, connection.Version);
        Assert.Empty(operations.Items);
    }

    [Fact]
    public async Task Update_exact_retry_replays_and_changed_intent_conflicts()
    {
        Guid propertyId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", "secret://initial", Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        UpdateAdapterConnectionCommandHandler handler =
            CreateUpdateHandler(connections, operations);
        UpdateAdapterConnectionCommand command = new(
            operationId,
            propertyId,
            connection.Id,
            AdapterExecutionMode.Continuous,
            AdapterConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged,
            " configuration://updated ",
            SecretReferenceUpdateMode.Replace,
            " secret://updated ",
            ExpectedVersion: 1);

        var updated = await handler.HandleAsync(command, CancellationToken.None);
        var replayed = await handler.HandleAsync(
            command with
            {
                ConfigurationReference = "configuration://updated",
                SecretReference = "secret://updated"
            },
            CancellationToken.None);
        var conflicted = await handler.HandleAsync(
            command with { ConfigurationReference = "configuration://changed" },
            CancellationToken.None);
        var stale = await handler.HandleAsync(
            command with { OperationId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.True(updated.IsSuccess, updated.Error.Code);
        Assert.Equal(updated.Value, replayed.Value);
        Assert.Equal(2, connection.Version);
        Assert.Equal("secret://updated", connection.SecretReference);
        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            conflicted.Error);
        Assert.Equal(
            BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.VersionConflict,
            stale.Error);
        IngestionConnectionManagementOperationRecord operation =
            Assert.Single(operations.Items);
        Assert.Equal(IngestionConnectionManagementMutationKind.ConnectionUpdate, operation.Kind);
        Assert.Equal(1, operation.ExpectedVersion);
        Assert.Equal(2, operation.ResultVersion);
    }

    [Fact]
    public async Task Update_no_change_receipt_does_not_resolve_keep_against_mutable_secret_state()
    {
        Guid propertyId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", "secret://initial", Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        UpdateAdapterConnectionCommandHandler handler =
            CreateUpdateHandler(connections, operations);
        UpdateAdapterConnectionCommand command = new(
            operationId,
            propertyId,
            connection.Id,
            AdapterExecutionMode.Polling,
            AdapterConflictPolicy.SuggestionsOnly,
            " configuration://main ",
            SecretReferenceUpdateMode.Keep,
            null,
            ExpectedVersion: 1);

        var noChange = await handler.HandleAsync(command, CancellationToken.None);
        Assert.True(noChange.IsSuccess, noChange.Error.Code);
        Assert.Equal(1, connection.Version);
        Assert.Equal(1, Assert.Single(operations.Items).ResultVersion);

        Assert.True(connection.Configure(
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main",
            "secret://changed-later",
            connection.Version,
            Now.AddMinutes(1)).IsSuccess);
        var replayed = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(replayed.IsSuccess, replayed.Error.Code);
        Assert.Equal(2, replayed.Value.Version);
        Assert.Single(operations.Items);
    }

    [Fact]
    public async Task Update_revalidates_lifecycle_and_country_policy_before_replay()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        UpdateAdapterConnectionCommand command = new(
            Guid.NewGuid(),
            propertyId,
            connection.Id,
            AdapterExecutionMode.Polling,
            AdapterConflictPolicy.SuggestionsOnly,
            "configuration://main",
            SecretReferenceUpdateMode.Keep,
            null,
            ExpectedVersion: 1);

        var completed = await CreateUpdateHandler(connections, operations)
            .HandleAsync(command, CancellationToken.None);
        var lifecycleDenied = await CreateUpdateHandler(
                connections,
                operations,
                lifecyclePolicies:
                [new TestLifecyclePolicy(IngestionTenantLifecycleDecision.Restricted)])
            .HandleAsync(command, CancellationToken.None);
        var countryDenied = await CreateUpdateHandler(
                connections,
                operations,
                new TestCountryPolicyAdmission(allowed: false))
            .HandleAsync(command, CancellationToken.None);

        Assert.True(completed.IsSuccess, completed.Error.Code);
        Assert.Equal(
            IngestionApplicationErrors.TenantLifecycleRestricted,
            lifecycleDenied.Error);
        Assert.Equal(
            IngestionApplicationErrors.CountryPolicyDenied(
                CountryPolicyDecisionReason.MissingBinding),
            countryDenied.Error);
        Assert.Single(operations.Items);
    }

    [Fact]
    public async Task Update_and_disable_use_property_scope_and_expected_version()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http", AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly, "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        IngestionExecutionMutationCoordinator execution =
            TestIngestionExecution.Create(connections);

        var updated = await CreateUpdateHandler(connections, operations).HandleAsync(
            new(Guid.NewGuid(), propertyId, connection.Id, AdapterExecutionMode.Continuous,
                AdapterConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged,
                "configuration://secondary", SecretReferenceUpdateMode.Replace, "secret://secondary", 1),
            CancellationToken.None);
        var wrongProperty = await new SetAdapterConnectionEnabledCommandHandler(
            execution, new TestCountryPolicyAdmission(allowed: false), new TestClock()).HandleAsync(
            new(Guid.NewGuid(), connection.Id, Enabled: false, ExpectedVersion: 2), CancellationToken.None);
        var disabled = await new SetAdapterConnectionEnabledCommandHandler(
            execution, new TestCountryPolicyAdmission(allowed: false), new TestClock()).HandleAsync(
            new(propertyId, connection.Id, Enabled: false, ExpectedVersion: 2), CancellationToken.None);
        var deniedEnable = await new SetAdapterConnectionEnabledCommandHandler(
            execution, new TestCountryPolicyAdmission(allowed: false), new TestClock()).HandleAsync(
            new(propertyId, connection.Id, Enabled: true, ExpectedVersion: 3), CancellationToken.None);

        Assert.True(updated.IsSuccess, updated.Error.Code);
        Assert.Equal(AdapterExecutionMode.Continuous, connection.ExecutionMode);
        Assert.Equal("secret://secondary", connection.SecretReference);
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
        FakeConnectionManagementOperationRepository operations = new();
        UpdateAdapterConnectionCommandHandler handler =
            CreateUpdateHandler(connections, operations);

        var kept = await handler.HandleAsync(new(
            Guid.NewGuid(), propertyId, connection.Id, AdapterExecutionMode.Continuous, AdapterConflictPolicy.SuggestionsOnly,
            "configuration://changed", SecretReferenceUpdateMode.Keep, null, 1), CancellationToken.None);
        Assert.True(kept.IsSuccess, kept.Error.Code);
        Assert.Equal("secret://initial", connection.SecretReference);

        Guid clearOperationId = Guid.NewGuid();
        var invalid = await handler.HandleAsync(new(
            clearOperationId, propertyId, connection.Id, AdapterExecutionMode.Continuous, AdapterConflictPolicy.SuggestionsOnly,
            "configuration://changed", SecretReferenceUpdateMode.Clear, "secret://unexpected", 2),
            CancellationToken.None);
        Assert.Equal(IngestionApplicationErrors.SecretReferenceUpdateInvalid, invalid.Error);
        Assert.Equal(2, connection.Version);

        var cleared = await handler.HandleAsync(new(
            clearOperationId, propertyId, connection.Id, AdapterExecutionMode.Continuous, AdapterConflictPolicy.SuggestionsOnly,
            "configuration://changed", SecretReferenceUpdateMode.Clear, null, 2), CancellationToken.None);

        Assert.True(cleared.IsSuccess, cleared.Error.Code);
        Assert.Null(connection.SecretReference);
        Assert.Equal(2, operations.Items.Count);
    }

    [Fact]
    public async Task Connection_configuration_requires_a_registered_compatible_adapter_capability()
    {
        Guid propertyId = Guid.NewGuid();
        FakeConnectionRepository connections = new();
        FakeConnectionManagementOperationRepository operations = new();
        CreateAdapterConnectionCommandHandler handler = new(
            connections, operations, TestIngestionExecution.Create(connections),
            new TestCountryPolicyAdmission(), new TestDescriptors(),
            new TestScope(), new TestClock());

        var unknown = await handler.HandleAsync(new(
            Guid.NewGuid(), propertyId, "missing.adapter", AdapterExecutionMode.Polling, AdapterConflictPolicy.SuggestionsOnly,
            "configuration://main", null), CancellationToken.None);
        var unsupported = await handler.HandleAsync(new(
            Guid.NewGuid(), propertyId, "fake.http", AdapterExecutionMode.Push, AdapterConflictPolicy.SuggestionsOnly,
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
        IngestionExecutionMutationCoordinator execution =
            TestIngestionExecution.Create(connections);

        var tooFrequent = await new ConfigureAdapterConnectionPollingScheduleCommandHandler(
            execution, descriptors, new TestClock()).HandleAsync(
            new(propertyId, connection.Id, IntervalSeconds: 60, MaxAttempts: 3, ExpectedVersion: 1),
            CancellationToken.None);
        var configured = await new ConfigureAdapterConnectionPollingScheduleCommandHandler(
            execution, descriptors, new TestClock()).HandleAsync(
            new(propertyId, connection.Id, IntervalSeconds: 180, MaxAttempts: 4, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.PollingIntervalBelowAdapterMinimum, tooFrequent.Error);
        Assert.True(configured.IsSuccess, configured.Error.Code);
        Assert.Equal(180, connection.PollingIntervalSeconds);
        Assert.Equal(4, connection.PollingScheduleMaxAttempts);
        Assert.NotNull(connection.PollingScheduleConfiguredAtUtc);

        var cleared = await new ClearAdapterConnectionPollingScheduleCommandHandler(
            execution, new TestClock()).HandleAsync(
            new(propertyId, connection.Id, configured.Value.Version), CancellationToken.None);

        Assert.True(cleared.IsSuccess, cleared.Error.Code);
        Assert.Null(connection.PollingIntervalSeconds);
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

    private static UpdateAdapterConnectionCommandHandler CreateUpdateHandler(
        FakeConnectionRepository connections,
        FakeConnectionManagementOperationRepository operations,
        IIngestionCountryPolicyAdmission? countryPolicy = null,
        IEnumerable<IIngestionTenantLifecyclePolicy>? lifecyclePolicies = null) =>
        new(
            TestIngestionExecution.Create(connections),
            operations,
            countryPolicy ?? new TestCountryPolicyAdmission(),
            new TestDescriptors(),
            new TestScope(),
            new TestClock(),
            lifecyclePolicies);

    private sealed class FakeConnectionManagementOperationRepository
        : IIngestionConnectionManagementOperationRepository
    {
        public List<IngestionConnectionManagementOperationRecord> Items { get; } = [];

        public Task<IngestionConnectionManagementOperationRecord?> GetAsync(
            Guid connectionId,
            Guid operationId,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Items.SingleOrDefault(item =>
                item.ConnectionId == connectionId &&
                item.OperationId == operationId));

        public Task AddAsync(
            IngestionConnectionManagementOperationRecord operation,
            CancellationToken cancellationToken)
        {
            this.Items.Add(operation);
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

    private sealed class EmptyRunRepository : IIngestionRunRepository
    {
        public Task<IngestionRun?> GetAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult<IngestionRun?>(null);

        public Task<IngestionRun?> FindByTaskExecutionAsync(
            Guid taskRunId, int taskAttempt, CancellationToken cancellationToken) =>
            Task.FromResult<IngestionRun?>(null);

        public Task<Guid?> FindByTaskExecutionIdAsync(
            Guid taskRunId, int taskAttempt, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);

        public Task<IngestionRun?> FindActiveByConnectionAsync(
            Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<IngestionRun?>(null);

        public Task<Guid?> FindActiveIdByConnectionAsync(
            Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);

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

    private sealed class TestLifecyclePolicy(
        IngestionTenantLifecycleDecision decision)
        : IIngestionTenantLifecyclePolicy
    {
        public ValueTask<IngestionTenantLifecycleDecision> AuthorizeAsync(
            string tenantId,
            IngestionTenantLifecycleOperation operation,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(decision);
    }
}
