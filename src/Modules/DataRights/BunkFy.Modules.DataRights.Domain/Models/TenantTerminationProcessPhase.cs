namespace BunkFy.Modules.DataRights.Domain.Models;

public enum TenantTerminationProcessPhase
{
    Unknown = 0,
    Freeze = 1,
    Export = 2,
    Destroy = 3,
    Verify = 4,
    Completed = 5,
    Restore = 6
}
