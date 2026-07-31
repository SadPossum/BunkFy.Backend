namespace BunkFy.Modules.DataRights.Domain.Models;

public enum TenantTerminationOwnerWorkState
{
    Unknown = 0,
    Prepared = 1,
    Processing = 2,
    RetryRequired = 3,
    Blocked = 4,
    Failed = 5,
    Completed = 6
}
