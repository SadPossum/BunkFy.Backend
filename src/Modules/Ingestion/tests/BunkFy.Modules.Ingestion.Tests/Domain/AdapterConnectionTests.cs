namespace BunkFy.Modules.Ingestion.Tests.Domain;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Errors;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterConnectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_adapter_metadata_and_keeps_only_references()
    {
        AdapterConnection connection = CreateConnection();

        Assert.Equal("ota.booking", connection.AdapterType);
        Assert.Equal("config/booking-main", connection.ConfigurationReference);
        Assert.Equal("secrets/booking-main", connection.SecretReference);
        Assert.Equal(IngestionConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged, connection.ConflictPolicy);
        Assert.Equal(AdapterConnectionState.Enabled, connection.State);
        Assert.Equal(1, connection.Version);
    }

    [Fact]
    public void Disabled_connection_cannot_advance_its_checkpoint()
    {
        AdapterConnection connection = CreateConnection();

        Assert.True(connection.Disable(1, Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(
            IngestionDomainErrors.ConnectionNotEnabled,
            connection.AdvanceCheckpoint("cursor-2", 2, Now.AddMinutes(2)).Error);
    }

    [Fact]
    public void Connection_updates_require_the_current_version()
    {
        AdapterConnection connection = CreateConnection();

        Assert.Equal(IngestionDomainErrors.VersionConflict, connection.Disable(2, Now).Error);
        Assert.Equal(AdapterConnectionState.Enabled, connection.State);
    }

    [Fact]
    public void Configuration_changes_are_versioned_and_keep_adapter_identity_immutable()
    {
        AdapterConnection connection = CreateConnection();

        Assert.True(connection.Configure(
            AdapterExecutionMode.Continuous,
            IngestionConflictPolicy.SuggestionsOnly,
            "config/booking-secondary",
            secretReference: null,
            expectedVersion: 1,
            Now.AddMinutes(1)).IsSuccess);

        Assert.Equal("ota.booking", connection.AdapterType);
        Assert.Equal(AdapterExecutionMode.Continuous, connection.ExecutionMode);
        Assert.Equal(IngestionConflictPolicy.SuggestionsOnly, connection.ConflictPolicy);
        Assert.Equal("config/booking-secondary", connection.ConfigurationReference);
        Assert.Null(connection.SecretReference);
        Assert.Equal(2, connection.Version);
    }

    [Fact]
    public void Checkpoint_reset_requires_a_disabled_connection_and_is_idempotent()
    {
        AdapterConnection connection = CreateConnection();
        Assert.True(connection.AdvanceCheckpoint("cursor-2", 1, Now.AddMinutes(1)).IsSuccess);

        Assert.Equal(
            IngestionDomainErrors.ConnectionMustBeDisabled,
            connection.ResetCheckpoint(2, Now.AddMinutes(2)).Error);
        Assert.True(connection.Disable(2, Now.AddMinutes(3)).IsSuccess);
        Assert.True(connection.ResetCheckpoint(3, Now.AddMinutes(4)).IsSuccess);
        Assert.Null(connection.Checkpoint);
        Assert.Equal(4, connection.Version);
        Assert.True(connection.ResetCheckpoint(4, Now.AddMinutes(5)).IsSuccess);
        Assert.Equal(4, connection.Version);
    }

    [Fact]
    public void Polling_schedule_is_versioned_explicit_and_blocks_incompatible_mode_changes()
    {
        AdapterConnection connection = CreateConnection();

        Assert.True(connection.ConfigurePollingSchedule(300, 3, 1, Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(300, connection.PollingIntervalSeconds);
        Assert.Equal(3, connection.PollingScheduleMaxAttempts);
        Assert.Equal(Now.AddMinutes(1), connection.PollingScheduleConfiguredAtUtc);
        Assert.Equal(2, connection.Version);

        var incompatible = connection.Configure(
            AdapterExecutionMode.Push, IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, 2, Now.AddMinutes(2));
        Assert.Equal(IngestionDomainErrors.PollingScheduleRequiresPollingMode, incompatible.Error);
        Assert.Equal(AdapterExecutionMode.Polling, connection.ExecutionMode);

        Assert.True(connection.ClearPollingSchedule(2, Now.AddMinutes(3)).IsSuccess);
        Assert.Null(connection.PollingIntervalSeconds);
        Assert.Null(connection.PollingScheduleMaxAttempts);
        Assert.Null(connection.PollingScheduleConfiguredAtUtc);
        Assert.True(connection.Configure(
            AdapterExecutionMode.Push, IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, 3, Now.AddMinutes(4)).IsSuccess);
    }

    [Fact]
    public void Polling_schedule_enforces_safe_module_bounds()
    {
        AdapterConnection connection = CreateConnection();

        Assert.Equal(IngestionDomainErrors.PollingIntervalInvalid,
            connection.ConfigurePollingSchedule(59, 3, 1, Now).Error);
        Assert.Equal(IngestionDomainErrors.PollingIntervalInvalid,
            connection.ConfigurePollingSchedule(AdapterConnection.MaximumPollingIntervalSeconds + 1, 3, 1, Now).Error);
        Assert.Equal(IngestionDomainErrors.PollingScheduleAttemptsInvalid,
            connection.ConfigurePollingSchedule(300, 0, 1, Now).Error);
        Assert.Equal(IngestionDomainErrors.PollingScheduleAttemptsInvalid,
            connection.ConfigurePollingSchedule(300, AdapterConnection.MaximumPollingScheduleAttempts + 1, 1, Now).Error);
        Assert.Equal(1, connection.Version);
    }

    private static AdapterConnection CreateConnection() => AdapterConnection.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        " OTA.Booking ",
        AdapterExecutionMode.Polling,
        IngestionConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged,
        " config/booking-main ",
        " secrets/booking-main ",
        Now).Value;
}
