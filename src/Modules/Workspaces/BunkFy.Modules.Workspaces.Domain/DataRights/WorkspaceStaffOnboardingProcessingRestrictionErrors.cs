namespace BunkFy.Modules.Workspaces.Domain.DataRights;

using Gma.Framework.Results;

public static class WorkspaceStaffOnboardingProcessingRestrictionErrors
{
    public static readonly Error IdentityInvalid = new(
        "Workspaces.StaffOnboardingRestrictionIdentityInvalid",
        "The Staff onboarding restriction identity is invalid.");
    public static readonly Error ApprovalInvalid = new(
        "Workspaces.StaffOnboardingRestrictionApprovalInvalid",
        "The Staff onboarding restriction approval is invalid.");
    public static readonly Error TransitionInvalid = new(
        "Workspaces.StaffOnboardingRestrictionTransitionInvalid",
        "The Staff onboarding restriction transition is invalid.");
    public static readonly Error VersionConflict = new(
        "Workspaces.StaffOnboardingRestrictionVersionConflict",
        "The Staff onboarding restriction version is no longer current.");
    public static readonly Error AlreadyReleased = new(
        "Workspaces.StaffOnboardingRestrictionAlreadyReleased",
        "The Staff onboarding restriction is already released.");
    public static readonly Error ProjectionIdentityInvalid = new(
        "Workspaces.StaffOnboardingRestrictionProjectionIdentityInvalid",
        "The Staff onboarding restriction projection identity is invalid.");
    public static readonly Error ProjectionContractUnsupported = new(
        "Workspaces.StaffOnboardingRestrictionProjectionContractUnsupported",
        "The Staff onboarding restriction projection contract is unsupported.");
    public static readonly Error ProjectionVersionConflict = new(
        "Workspaces.StaffOnboardingRestrictionProjectionVersionConflict",
        "The Staff onboarding restriction projection version is no longer current.");
    public static readonly Error ProjectionStateInvalid = new(
        "Workspaces.StaffOnboardingRestrictionProjectionStateInvalid",
        "The Staff onboarding restriction projection state is invalid.");
    public static readonly Error ProjectionTransitionInvalid = new(
        "Workspaces.StaffOnboardingRestrictionProjectionTransitionInvalid",
        "The Staff onboarding restriction projection transition is invalid.");
    public static readonly Error ReceiptIdentityInvalid = new(
        "Workspaces.StaffOnboardingRestrictionReceiptIdentityInvalid",
        "The Staff onboarding restriction receipt identity is invalid.");
    public static readonly Error ReceiptVersionInvalid = new(
        "Workspaces.StaffOnboardingRestrictionReceiptVersionInvalid",
        "The Staff onboarding restriction receipt version is invalid.");
    public static readonly Error ReceiptTransitionInvalid = new(
        "Workspaces.StaffOnboardingRestrictionReceiptTransitionInvalid",
        "The Staff onboarding restriction receipt transition is invalid.");
}
