namespace BunkFy.Modules.Ingestion.Domain.Connections;

public enum IngestionConflictPolicy
{
    Unknown = 0,
    SuggestionsOnly = 1,
    AutoApplyWhenAdapterBaselineUnchanged = 2
}
