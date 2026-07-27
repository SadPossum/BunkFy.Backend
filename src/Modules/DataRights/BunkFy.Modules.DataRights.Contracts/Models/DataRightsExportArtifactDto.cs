namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsExportArtifactDto(
    Guid Id,
    Guid CaseId,
    Guid? PropertyId,
    DataRightsCaseType CaseType,
    long DecisionRevision,
    int SelectedSubjectCount,
    DataRightsExportArtifactStatus Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? GenerationStartedAtUtc,
    DateTimeOffset? AvailableAtUtc,
    DateTimeOffset ExpiresAtUtc,
    long Version);
