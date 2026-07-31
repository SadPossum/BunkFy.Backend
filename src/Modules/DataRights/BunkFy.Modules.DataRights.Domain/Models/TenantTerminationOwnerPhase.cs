namespace BunkFy.Modules.DataRights.Domain.Models;

public enum TenantTerminationOwnerPhase
{
    Unknown = 0,
    Freeze = 1,
    Export = 2,
    Destroy = 3,
    Verify = 4,
    Restore = 5
}
