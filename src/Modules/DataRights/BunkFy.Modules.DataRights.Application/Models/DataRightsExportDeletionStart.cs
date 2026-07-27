namespace BunkFy.Modules.DataRights.Application.Models;

public sealed record DataRightsExportDeletionStart(
    bool DeletionRequired,
    Guid ArtifactId);
