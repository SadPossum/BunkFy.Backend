namespace BunkFy.Modules.Workspaces.Api.Requests;

public sealed record CreateWorkspaceAccessProfileRequest(
    Guid RequestId,
    string DisplayName,
    string? Description,
    IReadOnlyCollection<string>? Permissions);

public sealed record UpdateWorkspaceAccessProfileRequest(
    string DisplayName,
    string? Description,
    IReadOnlyCollection<string>? Permissions,
    long ExpectedVersion);

public sealed record ArchiveWorkspaceAccessProfileRequest(long ExpectedVersion);

public sealed record UpdateWorkspaceMemberAccessRequest(
    Guid ProfileId,
    IReadOnlyCollection<Guid>? PropertyIds);
