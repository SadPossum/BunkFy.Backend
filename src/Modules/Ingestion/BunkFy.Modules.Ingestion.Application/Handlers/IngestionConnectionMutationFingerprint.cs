namespace BunkFy.Modules.Ingestion.Application.Handlers;

using System.Globalization;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Domain.Connections;

internal static class IngestionConnectionMutationFingerprint
{
    public static string ComputeCreate(AdapterConnection connection) =>
        IngestionMutationFingerprint.Compute(
        "bunkfy-ingestion-connection-create/v1",
        connection.PropertyId.ToString("N"),
        connection.AdapterType,
        ((int)connection.ExecutionMode).ToString(CultureInfo.InvariantCulture),
        ((int)connection.ConflictPolicy).ToString(CultureInfo.InvariantCulture),
        connection.ConfigurationReference,
        connection.SecretReference ?? string.Empty);

    public static string ComputeUpdate(
        UpdateAdapterConnectionCommand command,
        IngestionConflictPolicy conflictPolicy) =>
        IngestionMutationFingerprint.Compute(
        "bunkfy-ingestion-connection-update/v1",
        command.PropertyId.ToString("N"),
        command.ConnectionId.ToString("N"),
        command.ExpectedVersion.ToString(CultureInfo.InvariantCulture),
        ((int)command.ExecutionMode).ToString(CultureInfo.InvariantCulture),
        ((int)conflictPolicy).ToString(CultureInfo.InvariantCulture),
        command.ConfigurationReference?.Trim() ?? string.Empty,
        ((int)command.SecretReferenceUpdateMode).ToString(
            CultureInfo.InvariantCulture),
        command.SecretReferenceUpdateMode == SecretReferenceUpdateMode.Replace
            ? command.SecretReference?.Trim() ?? string.Empty
            : string.Empty);

    public static string ComputeEnabledState(
        SetAdapterConnectionEnabledCommand command) =>
        IngestionMutationFingerprint.Compute(
        command.Enabled
            ? "bunkfy-ingestion-connection-enable/v1"
            : "bunkfy-ingestion-connection-disable/v1",
        command.PropertyId.ToString("N"),
        command.ConnectionId.ToString("N"),
        command.ExpectedVersion.ToString(CultureInfo.InvariantCulture));

    public static string ComputePollingSchedule(
        ConfigureAdapterConnectionPollingScheduleCommand command) =>
        IngestionMutationFingerprint.Compute(
        "bunkfy-ingestion-connection-polling-schedule-configure/v1",
        command.PropertyId.ToString("N"),
        command.ConnectionId.ToString("N"),
        command.ExpectedVersion.ToString(CultureInfo.InvariantCulture),
        command.IntervalSeconds.ToString(CultureInfo.InvariantCulture),
        command.MaxAttempts.ToString(CultureInfo.InvariantCulture));

    public static string ComputePollingScheduleClear(
        ClearAdapterConnectionPollingScheduleCommand command) =>
        ComputeVersionedControl(
            "bunkfy-ingestion-connection-polling-schedule-clear/v1",
            command.PropertyId,
            command.ConnectionId,
            command.ExpectedVersion);

    public static string ComputeCheckpointReset(
        ResetAdapterConnectionCheckpointCommand command) =>
        ComputeVersionedControl(
            "bunkfy-ingestion-connection-checkpoint-reset/v1",
            command.PropertyId,
            command.ConnectionId,
            command.ExpectedVersion);

    private static string ComputeVersionedControl(
        string schema,
        Guid propertyId,
        Guid connectionId,
        long expectedVersion) => IngestionMutationFingerprint.Compute(
        schema,
        propertyId.ToString("N"),
        connectionId.ToString("N"),
        expectedVersion.ToString(CultureInfo.InvariantCulture));
}
