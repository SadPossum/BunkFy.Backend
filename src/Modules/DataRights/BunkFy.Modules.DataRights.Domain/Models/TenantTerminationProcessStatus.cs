namespace BunkFy.Modules.DataRights.Domain.Models;

public enum TenantTerminationProcessStatus
{
    Unknown = 0,
    Pending = 1,
    Running = 2,
    Blocked = 3,
    Failed = 4,
    Completed = 5
}
