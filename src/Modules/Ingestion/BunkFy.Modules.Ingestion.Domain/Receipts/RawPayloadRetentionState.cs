namespace BunkFy.Modules.Ingestion.Domain.Receipts;

public enum RawPayloadRetentionState
{
    Unknown = 0,
    Available = 1,
    Purging = 2,
    Purged = 3
}
