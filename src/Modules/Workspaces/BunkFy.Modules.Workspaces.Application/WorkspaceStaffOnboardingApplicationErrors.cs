namespace BunkFy.Modules.Workspaces.Application;

using Gma.Framework.Results;

public static class WorkspaceStaffOnboardingApplicationErrors
{
    public static readonly Error ScopeRequired = new("Workspaces.ScopeRequired", "A workspace scope is required.");
    public static readonly Error VerifiedIdentityRequired = new("Workspaces.VerifiedIdentityRequired", "A verified account email is required.");
    public static readonly Error JoinTokenInvalid = new("Workspaces.JoinTokenInvalid", "The workspace join token is invalid or unavailable.");
    public static readonly Error ApplicationNotFound = new("Workspaces.StaffOnboardingNotFound", "The Staff onboarding application was not found.");
    public static readonly Error ProvisioningFailed = new("Workspaces.StaffOnboardingProvisioningFailed", "Staff onboarding provisioning did not complete.");
    public static readonly Error AccessPlanUnavailable = new(
        "Workspaces.StaffAccessPlanUnavailable",
        "The workspace Staff access plan is unavailable.");
    public static readonly Error RetentionCoordinateInvalid = new(
        "Workspaces.StaffOnboardingRetentionCoordinateInvalid",
        "The Staff onboarding retention coordinate is invalid.");
    public static readonly Error RetentionClaimInconsistent = new(
        "Workspaces.StaffOnboardingRetentionClaimInconsistent",
        "The authoritative enrollment claim is inconsistent with Staff onboarding.");
    public static readonly Error RetentionPlanInconsistent = new(
        "Workspaces.StaffOnboardingRetentionPlanInconsistent",
        "The Staff onboarding access plan is inconsistent with retention reconciliation.");
    public static readonly Error CorrectionRequestInvalid = new(
        "Workspaces.StaffOnboardingCorrectionRequestInvalid",
        "The Staff onboarding correction request is invalid.");
    public static readonly Error DataRightsApprovalRequired = new(
        "Workspaces.StaffOnboardingCorrectionApprovalRequired",
        "An active approved Data Rights correction execution is required.");
    public static readonly Error CorrectionTargetUnavailable = new(
        "Workspaces.StaffOnboardingCorrectionTargetUnavailable",
        "The selected Staff onboarding record is no longer editable.");
    public static readonly Error CorrectionIdempotencyConflict = new(
        "Workspaces.StaffOnboardingCorrectionIdempotencyConflict",
        "The Data Rights correction execution was already used for another request.");
}
