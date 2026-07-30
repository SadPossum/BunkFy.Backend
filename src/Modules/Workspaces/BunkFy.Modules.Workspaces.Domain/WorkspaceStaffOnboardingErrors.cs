namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Results;

public static class WorkspaceStaffOnboardingErrors
{
    public static readonly Error Invalid = new("Workspaces.StaffOnboardingInvalid", "The Staff onboarding application is invalid.");
    public static readonly Error Unavailable = new("Workspaces.StaffOnboardingUnavailable", "The Staff onboarding application is unavailable.");
    public static readonly Error ClaimConflict = new("Workspaces.StaffOnboardingClaimConflict", "The Staff onboarding application is bound to another claim.");
    public static readonly Error StateConflict = new("Workspaces.StaffOnboardingStateConflict", "The Staff onboarding application cannot make this transition.");
    public static readonly Error CorrectionVersionConflict = new(
        "Workspaces.StaffOnboardingCorrectionVersionConflict",
        "The selected Staff onboarding version is no longer current.");
    public static readonly Error CorrectionUnavailable = new(
        "Workspaces.StaffOnboardingCorrectionUnavailable",
        "The Staff onboarding application is no longer editable.");
    public static readonly Error CorrectionNoChanges = new(
        "Workspaces.StaffOnboardingCorrectionNoChanges",
        "The requested correction does not change the Staff onboarding application.");
    public static readonly Error CorrectionReceiptIdentityInvalid = new(
        "Workspaces.StaffOnboardingCorrectionReceiptIdentityInvalid",
        "The Staff onboarding correction proof identity is invalid.");
    public static readonly Error CorrectionReceiptVersionInvalid = new(
        "Workspaces.StaffOnboardingCorrectionReceiptVersionInvalid",
        "The Staff onboarding correction proof version is invalid.");
    public static readonly Error CorrectionReceiptFieldsInvalid = new(
        "Workspaces.StaffOnboardingCorrectionReceiptFieldsInvalid",
        "The Staff onboarding correction proof fields are invalid.");
}
