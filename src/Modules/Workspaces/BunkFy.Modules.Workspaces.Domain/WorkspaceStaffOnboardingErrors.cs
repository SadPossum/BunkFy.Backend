namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Results;

public static class WorkspaceStaffOnboardingErrors
{
    public static readonly Error Invalid = new("Workspaces.StaffOnboardingInvalid", "The Staff onboarding application is invalid.");
    public static readonly Error Unavailable = new("Workspaces.StaffOnboardingUnavailable", "The Staff onboarding application is unavailable.");
    public static readonly Error ClaimConflict = new("Workspaces.StaffOnboardingClaimConflict", "The Staff onboarding application is bound to another claim.");
    public static readonly Error StateConflict = new("Workspaces.StaffOnboardingStateConflict", "The Staff onboarding application cannot make this transition.");
}
