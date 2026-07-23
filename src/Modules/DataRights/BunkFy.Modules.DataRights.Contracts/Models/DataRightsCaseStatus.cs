namespace BunkFy.Modules.DataRights.Contracts;

public enum DataRightsCaseStatus
{
    Unknown = 0,
    Draft = 1,
    Discovery = 2,
    ReviewRequired = 3,
    DecisionPending = 4,
    Approved = 5,
    Denied = 6,
    Executing = 7,
    Blocked = 8,
    Completed = 9,
    PartiallyCompleted = 10,
    Canceled = 11
}
