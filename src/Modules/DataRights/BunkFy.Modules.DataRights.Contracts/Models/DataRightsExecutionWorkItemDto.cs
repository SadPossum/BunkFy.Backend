namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsExecutionWorkItemDto(
    Guid Id,
    Guid CaseId,
    Guid PropertyId,
    long ApprovalRevision,
    long ExecutionRevision,
    DataRightsOperation Operation,
    string OwnerKey,
    string RecordType,
    Guid RecordId,
    long SelectedRecordVersion,
    int PolicyEvidenceSchemaVersion,
    string PolicyId,
    int PolicyVersion,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string PolicyContentSha256,
    DataRightsExecutionWorkItemStatus Status,
    int AttemptCount,
    DateTimeOffset CreatedAtUtc,
    long Version);
