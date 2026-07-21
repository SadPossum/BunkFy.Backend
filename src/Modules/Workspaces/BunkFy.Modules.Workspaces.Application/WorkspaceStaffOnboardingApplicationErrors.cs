namespace BunkFy.Modules.Workspaces.Application;

using Gma.Framework.Results;

public static class WorkspaceStaffOnboardingApplicationErrors
{
    public static readonly Error ScopeRequired = new("Workspaces.ScopeRequired", "A workspace scope is required.");
    public static readonly Error VerifiedIdentityRequired = new("Workspaces.VerifiedIdentityRequired", "A verified account email is required.");
    public static readonly Error JoinTokenInvalid = new("Workspaces.JoinTokenInvalid", "The workspace join token is invalid or unavailable.");
    public static readonly Error ApplicationNotFound = new("Workspaces.StaffOnboardingNotFound", "The Staff onboarding application was not found.");
    public static readonly Error ProvisioningFailed = new("Workspaces.StaffOnboardingProvisioningFailed", "Staff onboarding provisioning did not complete.");
}
