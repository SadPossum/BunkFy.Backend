namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;

internal static class AdapterConnectionMappings
{
    public static AdapterConnectionDto Map(AdapterConnection connection) => new(
        connection.Id,
        connection.PropertyId,
        connection.AdapterType,
        connection.ExecutionMode,
        connection.PollingIntervalSeconds,
        connection.PollingScheduleMaxAttempts,
        connection.PollingScheduleConfiguredAtUtc,
        (AdapterConflictPolicy)(int)connection.ConflictPolicy,
        connection.ConfigurationReference,
        connection.SecretReference is not null,
        connection.Checkpoint,
        (AdapterConnectionStatus)(int)connection.State,
        connection.Version,
        connection.CreatedAtUtc,
        connection.UpdatedAtUtc);

    public static bool TryMap(AdapterConflictPolicy policy, out IngestionConflictPolicy mapped)
    {
        mapped = policy switch
        {
            AdapterConflictPolicy.SuggestionsOnly => IngestionConflictPolicy.SuggestionsOnly,
            AdapterConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged =>
                IngestionConflictPolicy.AutoApplyWhenAdapterBaselineUnchanged,
            _ => IngestionConflictPolicy.Unknown
        };
        return mapped != IngestionConflictPolicy.Unknown;
    }
}
