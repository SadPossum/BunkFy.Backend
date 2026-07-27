namespace BunkFy.Modules.Ingestion.Domain.Retention;

public enum IngestionRetentionExecutionState
{
    Unknown = 0,
    Running = 1,
    Completed = 2,
    Blocked = 3
}
