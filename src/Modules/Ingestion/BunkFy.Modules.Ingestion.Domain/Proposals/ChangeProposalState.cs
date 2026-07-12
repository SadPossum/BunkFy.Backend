namespace BunkFy.Modules.Ingestion.Domain.Proposals;

public enum ChangeProposalState
{
    Unknown = 0,
    Pending = 1,
    Applying = 2,
    Applied = 3,
    Rejected = 4,
    Superseded = 5,
    Stale = 6,
    Failed = 7
}
