namespace BunkFy.Modules.Ingestion.Domain.Reprocessing;

public enum ObservationReprocessingState
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    NoMatch = 4,
    Failed = 5,
    Canceled = 6,
    Expired = 7
}
