namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsResponseDeadlineEvidence(
    int SchemaVersion,
    Guid PropertyId,
    long PropertyTopologySourceVersion,
    long PropertyPolicySourceVersion,
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string ContentSha256,
    DataRightsResponseDeadlineRight ControllingRight,
    string RuleReference,
    int PeriodYears,
    int PeriodMonths,
    int PeriodDays,
    string TimeZoneId,
    DateTimeOffset PolicyEffectiveAtUtc,
    DateTimeOffset PolicyExpiresAtUtc,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset EvaluatedAtUtc,
    DateTimeOffset DueAtUtc);

public enum DataRightsResponseDeadlineRight
{
    Unknown = 0,
    Export = 1,
    Correction = 2,
    Restriction = 3,
    Erasure = 4
}
