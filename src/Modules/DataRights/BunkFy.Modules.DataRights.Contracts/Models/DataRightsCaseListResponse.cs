namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsCaseSummaryDto(
    Guid Id,
    Guid? PropertyId,
    DataRightsCaseType Type,
    DataRightsRequesterRelationship RequesterRelationship,
    DataRightsOperation RequestedOperations,
    DataRightsRestrictionDirective RestrictionDirective,
    DataRightsCaseStatus Status,
    int SelectedSubjectCount,
    DateTimeOffset? DueAtUtc,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);

public sealed record DataRightsCaseListResponse(
    IReadOnlyList<DataRightsCaseSummaryDto> Items,
    int Page,
    int PageSize,
    bool HasMore);
