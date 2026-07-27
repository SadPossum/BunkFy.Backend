namespace BunkFy.Modules.Retention.Domain.Models;

public enum RetentionExecutionState
{
    Unknown = 0,
    Running = 1,
    Completed = 2,
    Blocked = 3,
    Failed = 4
}
