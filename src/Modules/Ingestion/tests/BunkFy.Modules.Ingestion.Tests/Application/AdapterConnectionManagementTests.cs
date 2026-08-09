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
            execution, operations, new TestCountryPolicyAdmission(allowed: false),
            new TestScope(), new TestClock()).HandleAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), connection.Id, Enabled: false, ExpectedVersion: 2),
            CancellationToken.None);
        var disabled = await new SetAdapterConnectionEnabledCommandHandler(
            execution, operations, new TestCountryPolicyAdmission(allowed: false),
            new TestScope(), new TestClock()).HandleAsync(
            new(Guid.NewGuid(), propertyId, connection.Id, Enabled: false, ExpectedVersion: 2),
            CancellationToken.None);
        var deniedEnable = await new SetAdapterConnectionEnabledCommandHandler(
            execution, operations, new TestCountryPolicyAdmission(allowed: false),
            new TestScope(), new TestClock()).HandleAsync(
            new(Guid.NewGuid(), propertyId, connection.Id, Enabled: true, ExpectedVersion: 3),
            CancellationToken.None);

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
        FakeConnectionManagementOperationRepository operations = new();
        TestDescriptors descriptors = new();
        IngestionExecutionMutationCoordinator execution =
            TestIngestionExecution.Create(connections);

        var tooFrequent = await new ConfigureAdapterConnectionPollingScheduleCommandHandler(
            execution, operations, descriptors, new TestScope(), new TestClock()).HandleAsync(
            new(Guid.NewGuid(), propertyId, connection.Id, IntervalSeconds: 60, MaxAttempts: 3, ExpectedVersion: 1),
            CancellationToken.None);
        var configured = await new ConfigureAdapterConnectionPollingScheduleCommandHandler(
            execution, operations, descriptors, new TestScope(), new TestClock()).HandleAsync(
            new(Guid.NewGuid(), propertyId, connection.Id, IntervalSeconds: 180, MaxAttempts: 4, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.PollingIntervalBelowAdapterMinimum, tooFrequent.Error);
        Assert.True(configured.IsSuccess, configured.Error.Code);
        Assert.Equal(180, connection.PollingIntervalSeconds);
        Assert.Equal(4, connection.PollingScheduleMaxAttempts);
        Assert.NotNull(connection.PollingScheduleConfiguredAtUtc);

        var cleared = await new ClearAdapterConnectionPollingScheduleCommandHandler(
            execution, operations, new TestScope(), new TestClock()).HandleAsync(
            new(Guid.NewGuid(), propertyId, connection.Id, configured.Value.Version),
            CancellationToken.None);

        Assert.True(cleared.IsSuccess, cleared.Error.Code);
        Assert.Null(connection.PollingIntervalSeconds);
        Assert.Equal(3, cleared.Value.Version);
    }

    [Fact]
    public async Task Enabled_state_exact_retries_replay_and_changed_intent_conflicts()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        SetAdapterConnectionEnabledCommandHandler handler = new(
            TestIngestionExecution.Create(connections),
            operations,
            new TestCountryPolicyAdmission(),
            new TestScope(),
            new TestClock());
        SetAdapterConnectionEnabledCommand disable = new(
            Guid.NewGuid(),
            propertyId,
            connection.Id,
            Enabled: false,
            ExpectedVersion: 1);

        var disabled = await handler.HandleAsync(disable, CancellationToken.None);
        var replayedDisable = await handler.HandleAsync(
            disable,
            CancellationToken.None);
        var conflicted = await handler.HandleAsync(
            disable with { Enabled = true },
            CancellationToken.None);
        SetAdapterConnectionEnabledCommand enable = new(
            Guid.NewGuid(),
            propertyId,
            connection.Id,
            Enabled: true,
            ExpectedVersion: 2);
        var enabled = await handler.HandleAsync(enable, CancellationToken.None);
        var replayedEnable = await handler.HandleAsync(
            enable,
            CancellationToken.None);

        Assert.True(disabled.IsSuccess, disabled.Error.Code);
        Assert.Equal(disabled.Value, replayedDisable.Value);
        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            conflicted.Error);
        Assert.True(enabled.IsSuccess, enabled.Error.Code);
        Assert.Equal(enabled.Value, replayedEnable.Value);
        Assert.Equal(3, connection.Version);
        Assert.Equal(AdapterConnectionState.Enabled, connection.State);
        Assert.Collection(
            operations.Items,
            operation => Assert.Equal(
                IngestionConnectionManagementMutationKind.ConnectionDisable,
                operation.Kind),
            operation => Assert.Equal(
                IngestionConnectionManagementMutationKind.ConnectionEnable,
                operation.Kind));
    }

    [Fact]
    public async Task Enabled_state_revalidates_lifecycle_and_country_policy_before_replay()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        IngestionExecutionMutationCoordinator execution =
            TestIngestionExecution.Create(connections);
        SetAdapterConnectionEnabledCommand disable = new(
            Guid.NewGuid(), propertyId, connection.Id, false, 1);
        var completedDisable = await new SetAdapterConnectionEnabledCommandHandler(
            execution,
            operations,
            new TestCountryPolicyAdmission(),
            new TestScope(),
            new TestClock()).HandleAsync(disable, CancellationToken.None);
        var lifecycleDenied = await new SetAdapterConnectionEnabledCommandHandler(
            execution,
            operations,
            new TestCountryPolicyAdmission(),
            new TestScope(),
            new TestClock(),
            [new TestLifecyclePolicy(
                IngestionTenantLifecycleDecision.Restricted)])
            .HandleAsync(disable, CancellationToken.None);
        SetAdapterConnectionEnabledCommand enable = new(
            Guid.NewGuid(), propertyId, connection.Id, true, 2);
        var completedEnable = await new SetAdapterConnectionEnabledCommandHandler(
            execution,
            operations,
            new TestCountryPolicyAdmission(),
            new TestScope(),
            new TestClock()).HandleAsync(enable, CancellationToken.None);
        var countryDenied = await new SetAdapterConnectionEnabledCommandHandler(
            execution,
            operations,
            new TestCountryPolicyAdmission(allowed: false),
            new TestScope(),
            new TestClock()).HandleAsync(enable, CancellationToken.None);

        Assert.True(completedDisable.IsSuccess, completedDisable.Error.Code);
        Assert.Equal(
            IngestionApplicationErrors.TenantLifecycleRestricted,
            lifecycleDenied.Error);
        Assert.True(completedEnable.IsSuccess, completedEnable.Error.Code);
        Assert.Equal(
            IngestionApplicationErrors.CountryPolicyDenied(
                CountryPolicyDecisionReason.MissingBinding),
            countryDenied.Error);
        Assert.Equal(2, operations.Items.Count);
    }

    [Fact]
    public async Task Polling_schedule_controls_replay_conflict_and_record_no_change()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        IngestionExecutionMutationCoordinator execution =
            TestIngestionExecution.Create(connections);
        ConfigureAdapterConnectionPollingScheduleCommandHandler configure = new(
            execution,
            operations,
            new TestDescriptors(),
            new TestScope(),
            new TestClock());
        ClearAdapterConnectionPollingScheduleCommandHandler clear = new(
            execution,
            operations,
            new TestScope(),
            new TestClock());
        ResetAdapterConnectionCheckpointCommandHandler reset = new(
            execution,
            operations,
            new TestScope(),
            new TestClock());
        ConfigureAdapterConnectionPollingScheduleCommand configuredCommand = new(
            Guid.NewGuid(), propertyId, connection.Id, 180, 4, 1);

        var configured = await configure.HandleAsync(
            configuredCommand,
            CancellationToken.None);
        var replayedConfigure = await configure.HandleAsync(
            configuredCommand,
            CancellationToken.None);
        var changedConfigure = await configure.HandleAsync(
            configuredCommand with { IntervalSeconds = 240 },
            CancellationToken.None);
        var noChangeConfigure = await configure.HandleAsync(
            configuredCommand with
            {
                OperationId = Guid.NewGuid(),
                ExpectedVersion = 2
            },
            CancellationToken.None);
        ClearAdapterConnectionPollingScheduleCommand clearCommand = new(
            Guid.NewGuid(), propertyId, connection.Id, 2);
        var cleared = await clear.HandleAsync(clearCommand, CancellationToken.None);
        var replayedClear = await clear.HandleAsync(
            clearCommand,
            CancellationToken.None);
        var changedKind = await reset.HandleAsync(
            new ResetAdapterConnectionCheckpointCommand(
                clearCommand.OperationId,
                propertyId,
                connection.Id,
                clearCommand.ExpectedVersion),
            CancellationToken.None);
        var noChangeClear = await clear.HandleAsync(
            clearCommand with
            {
                OperationId = Guid.NewGuid(),
                ExpectedVersion = 3
            },
            CancellationToken.None);

        Assert.True(configured.IsSuccess, configured.Error.Code);
        Assert.Equal(configured.Value, replayedConfigure.Value);
        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            changedConfigure.Error);
        Assert.True(noChangeConfigure.IsSuccess, noChangeConfigure.Error.Code);
        Assert.Equal(2, noChangeConfigure.Value.Version);
        Assert.True(cleared.IsSuccess, cleared.Error.Code);
        Assert.Equal(cleared.Value, replayedClear.Value);
        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationConflict,
            changedKind.Error);
        Assert.True(noChangeClear.IsSuccess, noChangeClear.Error.Code);
        Assert.Equal(3, noChangeClear.Value.Version);
        Assert.Equal(4, operations.Items.Count);
        Assert.Contains(
            operations.Items,
            operation => operation.ResultVersion == operation.ExpectedVersion);
    }

    [Fact]
    public async Task Polling_schedule_revalidates_operation_lifecycle_and_capability_before_replay()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        IngestionExecutionMutationCoordinator execution =
            TestIngestionExecution.Create(connections);
        ConfigureAdapterConnectionPollingScheduleCommand command = new(
            Guid.NewGuid(), propertyId, connection.Id, 180, 3, 1);
        ConfigureAdapterConnectionPollingScheduleCommandHandler handler = new(
            execution,
            operations,
            new TestDescriptors(),
            new TestScope(),
            new TestClock());

        var missingOperation = await handler.HandleAsync(
            command with { OperationId = Guid.Empty },
            CancellationToken.None);
        var completed = await handler.HandleAsync(command, CancellationToken.None);
        var lifecycleDenied =
            await new ConfigureAdapterConnectionPollingScheduleCommandHandler(
                execution,
                operations,
                new TestDescriptors(),
                new TestScope(),
                new TestClock(),
                [new TestLifecyclePolicy(
                    IngestionTenantLifecycleDecision.Restricted)])
                .HandleAsync(command, CancellationToken.None);
        var capabilityDenied =
            await new ConfigureAdapterConnectionPollingScheduleCommandHandler(
                execution,
                operations,
                new EmptyDescriptors(),
                new TestScope(),
                new TestClock())
                .HandleAsync(command, CancellationToken.None);

        Assert.Equal(
            IngestionApplicationErrors.ConnectionManagementOperationInvalid,
            missingOperation.Error);
        Assert.True(completed.IsSuccess, completed.Error.Code);
        Assert.Equal(
            IngestionApplicationErrors.TenantLifecycleRestricted,
            lifecycleDenied.Error);
        Assert.Equal(
            IngestionApplicationErrors.AdapterTypeNotRegistered,
            capabilityDenied.Error);
        Assert.Single(operations.Items);
    }

    [Fact]
    public async Task Checkpoint_reset_replays_and_records_an_already_clear_checkpoint()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, Now).Value;
        Assert.True(connection.AdvanceCheckpoint(
            "checkpoint-1", connection.Version, Now).IsSuccess);
        Assert.True(connection.Disable(
            connection.Version, Now.AddSeconds(1)).IsSuccess);
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        ResetAdapterConnectionCheckpointCommandHandler handler = new(
            TestIngestionExecution.Create(connections),
            operations,
            new TestScope(),
            new TestClock());
        ResetAdapterConnectionCheckpointCommand command = new(
            Guid.NewGuid(), propertyId, connection.Id, connection.Version);

        var reset = await handler.HandleAsync(command, CancellationToken.None);
        var replayed = await handler.HandleAsync(command, CancellationToken.None);
        var noChange = await handler.HandleAsync(
            command with
            {
                OperationId = Guid.NewGuid(),
                ExpectedVersion = connection.Version
            },
            CancellationToken.None);

        Assert.True(reset.IsSuccess, reset.Error.Code);
        Assert.Equal(reset.Value, replayed.Value);
        Assert.Null(connection.Checkpoint);
        Assert.True(noChange.IsSuccess, noChange.Error.Code);
        Assert.Equal(reset.Value.Version, noChange.Value.Version);
        Assert.Equal(2, operations.Items.Count);
        Assert.Equal(
            noChange.Value.Version,
            operations.Items[^1].ResultVersion);
    }

    [Fact]
    public async Task Disable_replay_does_not_touch_a_later_remote_run()
    {
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.remote",
            AdapterExecutionMode.RemotePolling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, Now).Value;
        FakeConnectionRepository connections = new(connection);
        FakeConnectionManagementOperationRepository operations = new();
        TrackingRunRepository runs = new();
        SetAdapterConnectionEnabledCommandHandler handler = new(
            TestIngestionExecution.Create(connections, runs),
            operations,
            new TestCountryPolicyAdmission(),
            new TestScope(),
            new TestClock());
        SetAdapterConnectionEnabledCommand disable = new(
            Guid.NewGuid(), propertyId, connection.Id, false, 1);
        var completed = await handler.HandleAsync(disable, CancellationToken.None);
        Assert.True(completed.IsSuccess, completed.Error.Code);
        Assert.True(connection.Enable(
            connection.Version, Now.AddSeconds(1)).IsSuccess);

        Guid runId = Guid.NewGuid();
        Guid leaseId = Guid.NewGuid();
        Guid claimId = Guid.NewGuid();
        Guid credentialId = Guid.NewGuid();
        Guid workerId = Guid.NewGuid();
        var lease = connection.ClaimRemoteLease(
            runId,
            leaseId,
            claimId,
            credentialId,
            workerId,
            TimeSpan.FromMinutes(2),
            connection.Version,
            Now.AddSeconds(2));
        Assert.True(lease.IsSuccess, lease.Error.Code);
        runs.Item = IngestionRun.StartRemote(
            runId,
            "tenant-a",
            connection.Id,
            propertyId,
            leaseId,
            claimId,
            lease.Value.LeaseEpoch,
            credentialId,
            workerId,
            connection.Checkpoint,
            lease.Value.ExpiresAtUtc,
            Now.AddSeconds(2)).Value;

        var replayed = await handler.HandleAsync(disable, CancellationToken.None);

        Assert.True(replayed.IsSuccess, replayed.Error.Code);
        Assert.Equal(connection.Version, replayed.Value.Version);
        Assert.Equal(AdapterConnectionState.Enabled, connection.State);
        Assert.NotNull(connection.RemoteLeaseId);
        Assert.Equal(IngestionRunState.Running, runs.Item.State);
        Assert.Equal(0, runs.GetCalls);
        Assert.Single(operations.Items);
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

    private sealed class TrackingRunRepository : IIngestionRunRepository
    {
        public IngestionRun Item { get; set; } = null!;
        public int GetCalls { get; private set; }

        public Task<IngestionRun?> GetAsync(
            Guid runId,
            CancellationToken cancellationToken)
        {
            this.GetCalls++;
            return Task.FromResult(
                this.Item?.Id == runId ? this.Item : null);
        }

        public Task<IngestionRun?> FindByTaskExecutionAsync(
            Guid taskRunId,
            int taskAttempt,
            CancellationToken cancellationToken) =>
            Task.FromResult<IngestionRun?>(null);

        public Task<Guid?> FindByTaskExecutionIdAsync(
            Guid taskRunId,
            int taskAttempt,
            CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);

        public Task<IngestionRun?> FindActiveByConnectionAsync(
            Guid connectionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IngestionRun?>(null);

        public Task<Guid?> FindActiveIdByConnectionAsync(
            Guid connectionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);

        public Task AddAsync(
            IngestionRun run,
            CancellationToken cancellationToken) =>
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

    private sealed class EmptyDescriptors : IAdapterDescriptorRegistry
    {
        public IReadOnlyCollection<AdapterDescriptor> GetAll() => [];

        public bool TryGet(
            string adapterType,
            out AdapterDescriptor? descriptor)
        {
            descriptor = null;
            return false;
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
