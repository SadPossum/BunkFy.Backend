namespace BunkFy.Modules.DataRights.Contracts;

public enum DataRightsExportArtifactStatus
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
