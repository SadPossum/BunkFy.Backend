namespace BunkFy.Modules.Reservations.Domain.Retention;

public enum ReservationRetentionExecutionState
{
    Running = 1,
    Completed = 2,
    Blocked = 3,
    Failed = 4
}
