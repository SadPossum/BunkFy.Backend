namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsSelectedSubjectDto(
    string OwnerKey,
    string RecordType,
    Guid RecordId,
    long RecordVersion,
    DateTimeOffset SelectedAtUtc);

public sealed record DataRightsSelectedSubjectsResponse(
    long CaseVersion,
    IReadOnlyCollection<DataRightsSelectedSubjectDto> Subjects);
