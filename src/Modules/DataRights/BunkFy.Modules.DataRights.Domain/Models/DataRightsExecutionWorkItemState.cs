namespace BunkFy.Modules.DataRights.Domain.Models;

public enum DataRightsExecutionWorkItemState
{
    Unknown = 0,
    Prepared = 1,
    Processing = 2,
    Blocked = 3,
    Failed = 4,
    Completed = 5,
    NoOp = 6
}
