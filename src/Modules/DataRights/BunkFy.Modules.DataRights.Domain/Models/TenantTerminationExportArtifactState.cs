namespace BunkFy.Modules.DataRights.Domain.Models;

public enum TenantTerminationExportArtifactState
{
    Unknown = 0,
    Requested = 1,
    Generating = 2,
    Available = 3,
    Failed = 4,
    Expired = 5,
    Deleting = 6,
    Deleted = 7
}
