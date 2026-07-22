namespace BunkFy.Modules.Workspaces.Application;

using Gma.Framework.Results;

public static class WorkspaceAccessManagementErrors
{
    public static readonly Error ScopeRequired = new(
        "Workspaces.AccessManagementScopeRequired",
        "An active workspace scope is required.");

    public static readonly Error PermissionsInvalid = new(
        "Workspaces.AccessProfilePermissionsInvalid",
        "One or more selected permissions are not available to workspace access profiles.");

    public static readonly Error PermissionDependencyMissing = new(
        "Workspaces.AccessProfilePermissionDependencyMissing",
        "One or more selected permissions require another permission that was not selected.");

    public static readonly Error SeedProfileProtected = new(
        "Workspaces.AccessProfileSeedProtected",
        "Seed workspace access profiles cannot be archived.");

    public static readonly Error ProfileAssigned = new(
        "Workspaces.AccessProfileAssigned",
        "Remove this profile from every staff member before archiving it.");

    public static readonly Error RequestInvalid = new(
        "Workspaces.AccessProfileRequestInvalid",
        "A non-empty profile request id is required.");

    public static readonly Error RequestConflict = new(
        "Workspaces.AccessProfileRequestConflict",
        "The profile request id has already been used with different values.");

    public static readonly Error MemberInvalid = new(
        "Workspaces.AccessMemberInvalid",
        "A valid user subject id is required.");

    public static readonly Error MemberUnavailable = new(
        "Workspaces.AccessMemberUnavailable",
        "The requested subject is not an active member of this workspace.");

    public static readonly Error OwnerProtected = new(
        "Workspaces.AccessOwnerProtected",
        "Workspace owner access cannot be changed through staff role management.");

    public static readonly Error ProfileUnavailable = new(
        "Workspaces.AccessProfileUnavailable",
        "The selected access profile is not active and delegable in this workspace.");

    public static readonly Error PropertiesInvalid = new(
        "Workspaces.AccessPropertiesInvalid",
        "Property assignments must contain distinct, non-empty property ids.");

    public static readonly Error PropertyUnavailable = new(
        "Workspaces.AccessPropertyUnavailable",
        "One or more selected properties are not active in this workspace.");

    public static readonly Error JoinSourceRequestInvalid = new(
        "Workspaces.JoinSourceRequestInvalid",
        "The join-source request is invalid.");

    public static readonly Error JoinSourceManagementFailed = new(
        "Workspaces.JoinSourceManagementFailed",
        "The workspace join-source operation could not be completed.");

    public static readonly Error JoinSourcePlanUnavailable = new(
        "Workspaces.JoinSourcePlanUnavailable",
        "The join source does not have a reusable workspace access plan.");

    public static readonly Error JoinSourceReplacementUnavailable = new(
        "Workspaces.JoinSourceReplacementUnavailable",
        "The join source cannot be replaced from its current state or version.");
}
