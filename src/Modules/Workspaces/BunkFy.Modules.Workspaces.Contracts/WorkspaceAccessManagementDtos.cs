namespace BunkFy.Modules.Workspaces.Contracts;

public sealed record WorkspaceAccessProfileDto(
    Guid ProfileId,
    string Key,
    string DisplayName,
    string Description,
    WorkspaceAccessProfileStatus Status,
    long Version,
    IReadOnlyList<string> Permissions,
    int AssignmentCount,
    bool IsSeed,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);

public sealed record WorkspaceAccessProfileListResponse(
    IReadOnlyList<WorkspaceAccessProfileDto> Items,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record WorkspaceAccessPermissionDto(
    string Code,
    string Group,
    string Label,
    string Description,
    bool IsSensitive,
    IReadOnlyList<string> RequiredPermissions);

public sealed record WorkspaceAccessCatalogueDto(
    IReadOnlyList<WorkspaceAccessPermissionDto> Permissions,
    IReadOnlyList<string> ProtectedSeedKeys);

public sealed record WorkspaceMemberAccessDto(
    string SubjectId,
    IReadOnlyList<WorkspaceMemberAccessAssignmentDto> Assignments);

public sealed record WorkspaceMemberAccessAssignmentDto(
    Guid ProfileId,
    string ProfileKey,
    string ProfileDisplayName,
    long ProfileVersion,
    Guid? PropertyId);

public sealed record WorkspaceStaffJoinSourceDto(
    Guid SourceId,
    WorkspaceStaffOnboardingSourceKind SourceKind,
    string? RecipientEmail,
    DateTimeOffset ExpiresAtUtc,
    WorkspaceStaffJoinSourceStatus Status,
    long Version,
    int? MaximumClaims,
    int? ReservedClaims,
    string? ApprovalMode,
    WorkspaceStaffAccessPlanDto? AccessPlan,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);

public sealed record WorkspaceStaffJoinSourceListResponse(
    IReadOnlyList<WorkspaceStaffJoinSourceDto> Items,
    int Page,
    int PageSize);

public sealed record WorkspaceStaffJoinSourceReplacementDto(
    Guid PreviousSourceId,
    WorkspaceStaffJoinSourceStatus PreviousStatus,
    long PreviousVersion,
    WorkspaceStaffJoinSourceIssuanceDto Replacement);

public enum WorkspaceAccessProfileStatus
{
    Unknown = 0,
    Active = 1,
    Archived = 2
}

public enum WorkspaceStaffJoinSourceStatus
{
    Unknown = 0,
    Active = 1,
    Accepted = 2,
    Revoked = 3,
    Superseded = 4,
    Expired = 5,
    Disabled = 6,
    CapacityReached = 7
}
