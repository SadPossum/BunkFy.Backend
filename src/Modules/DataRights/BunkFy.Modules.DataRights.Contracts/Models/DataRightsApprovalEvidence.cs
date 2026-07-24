namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsApprovalEvidence(
    int SchemaVersion,
    Guid PropertyId,
    long PropertyVersion,
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    string PurposeCode,
    string Surface,
    string SourceProvenance,
    DateTimeOffset EvaluatedAtUtc,
    bool RequiresDistinctExecutor);
