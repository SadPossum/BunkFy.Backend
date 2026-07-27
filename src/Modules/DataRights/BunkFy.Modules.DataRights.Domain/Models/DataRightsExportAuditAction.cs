namespace BunkFy.Modules.DataRights.Domain.Models;

public enum DataRightsExportAuditAction
{
    Unknown = 0,
    GenerationRequested = 1,
    GenerationStarted = 2,
    GenerationCompleted = 3,
    GenerationFailed = 4,
    Download = 5,
    Expired = 6,
    DeletionStarted = 7,
    Deleted = 8
}
