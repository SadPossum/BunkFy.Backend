namespace BunkFy.Modules.Workspaces.Api.Requests;

using Gma.Modules.Organizations.Contracts;

public sealed record IssueWorkspaceInvitationRequest(
    Guid SourceId,
    string? RecipientEmail,
    int LifetimeHours,
    string ProfileKey,
    IReadOnlyCollection<Guid>? PropertyIds);

public sealed record IssueWorkspaceEnrollmentLinkRequest(
    Guid SourceId,
    int LifetimeHours,
    int MaximumClaims,
    OrganizationEnrollmentApprovalMode ApprovalMode,
    string ProfileKey,
    IReadOnlyCollection<Guid>? PropertyIds);
