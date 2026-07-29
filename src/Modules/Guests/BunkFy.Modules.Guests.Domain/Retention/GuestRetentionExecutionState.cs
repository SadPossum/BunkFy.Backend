namespace BunkFy.Modules.Guests.Domain.Retention;

public enum GuestRetentionExecutionState
{
    Unknown = 0,
    Running = 1,
    Completed = 2,
    Blocked = 3,
    Failed = 4
}
