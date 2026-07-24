namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsCaseDto(
    Guid Id,
    Guid? PropertyId,
    DataRightsCaseType Type,
    DataRightsOperation RequestedOperations,
    DataRightsRequesterRelationship RequesterRelationship,
    DataRightsVerificationStatus VerificationStatus,
    DataRightsRoutingStatus RoutingStatus,
    DataRightsCaseStatus Status,
    int SelectedSubjectCount,
    DateTimeOffset? DueAtUtc,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);
