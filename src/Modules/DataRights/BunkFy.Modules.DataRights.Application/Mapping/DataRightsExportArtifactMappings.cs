namespace BunkFy.Modules.DataRights.Application.Mapping;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public static class DataRightsExportArtifactMappings
{
    public static DataRightsExportArtifactDto ToDto(
        this DataRightsExportArtifact artifact) =>
        artifact.ToDto((DataRightsExportArtifactStatus)artifact.State);

    public static DataRightsExportArtifactDto ToDto(
        this DataRightsExportArtifact artifact,
        DataRightsExportArtifactStatus status) => new(
            artifact.Id,
            artifact.CaseId,
            artifact.PropertyId,
            (DataRightsCaseType)artifact.CaseKind,
            artifact.DecisionRevision,
            artifact.SelectedSubjectCount,
            status,
            artifact.RequestedAtUtc,
            artifact.GenerationStartedAtUtc,
            artifact.AvailableAtUtc,
            artifact.ExpiresAtUtc,
            artifact.Version);
}
