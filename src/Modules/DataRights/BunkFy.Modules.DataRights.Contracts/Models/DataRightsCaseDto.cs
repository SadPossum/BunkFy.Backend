namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsCaseDto(
    Guid Id,
    Guid? PropertyId,
    DataRightsCaseType Type,
    DataRightsOperation RequestedOperations,
    DataRightsRestrictionDirective RestrictionDirective,
    DataRightsRequesterRelationship RequesterRelationship,
    DataRightsVerificationStatus VerificationStatus,
    DataRightsRoutingStatus RoutingStatus,
    DataRightsCaseStatus Status,
    DataRightsDecisionOutcome Decision,
    DataRightsDecisionReason DecisionReason,
    long? DecisionRevision,
    DateTimeOffset? DecidedAtUtc,
    int SelectedSubjectCount,
    DateTimeOffset? DueAtUtc,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);
