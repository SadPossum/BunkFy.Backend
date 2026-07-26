namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsExecutionBatchDto(
    Guid Id,
    Guid CaseId,
    Guid PropertyId,
    long ApprovalRevision,
    long ExecutionRevision,
    int SelectedSubjectCount,
    DateTimeOffset CreatedAtUtc,
    long Version);
