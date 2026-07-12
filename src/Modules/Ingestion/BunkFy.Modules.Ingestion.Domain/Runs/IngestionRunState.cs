namespace BunkFy.Modules.Ingestion.Domain.Runs;

public enum IngestionRunState
{
    Unknown = 0,
    Running = 1,
    Succeeded = 2,
    PartiallySucceeded = 3,
    Failed = 4,
    Cancelled = 5
}
