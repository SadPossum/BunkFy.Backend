namespace BunkFy.Modules.Workspaces.Contracts;

public sealed record WorkspaceStaffAccessProcessDto(
    Guid ProcessId,
    Guid OrganizationId,
    Guid StaffMemberId,
    WorkspaceStaffAccessTargetStatus TargetStatus,
    long TargetStaffVersion,
    DateOnly EffectiveOn,
    WorkspaceStaffAccessProcessStatus Status,
    int ProfileCount,
    string? FailureCode,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record WorkspaceStaffAccessProcessListResponse(
    IReadOnlyList<WorkspaceStaffAccessProcessDto> Items,
    int Page,
    int PageSize);

public enum WorkspaceStaffAccessTargetStatus
{
    Unknown = 0,
    Active = 1,
    Suspended = 2,
    Departed = 3
}

public enum WorkspaceStaffAccessProcessStatus
{
    Unknown = 0,
    Prepared = 1,
    AwaitingStaffCommit = 2,
    RestorationPending = 3,
    Completed = 4
}
