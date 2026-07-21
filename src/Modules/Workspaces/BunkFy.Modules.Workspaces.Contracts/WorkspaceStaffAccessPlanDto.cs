namespace BunkFy.Modules.Workspaces.Contracts;

public sealed record WorkspaceStaffAccessPlanDto(
    Guid SourceId,
    WorkspaceStaffOnboardingSourceKind SourceKind,
    Guid ProfileId,
    string ProfileKey,
    IReadOnlyCollection<Guid> PropertyIds,
    WorkspaceStaffAccessPlanStatus Status,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);

public enum WorkspaceStaffAccessPlanStatus
{
    Unknown = 0,
    Prepared = 1,
    Active = 2,
    Superseded = 3
}

public sealed record WorkspaceStaffJoinSourceIssuanceDto(
    WorkspaceStaffAccessPlanDto Plan,
    string? Token,
    bool AlreadyIssued);
