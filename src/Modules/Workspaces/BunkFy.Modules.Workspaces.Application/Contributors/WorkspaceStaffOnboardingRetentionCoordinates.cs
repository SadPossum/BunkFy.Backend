namespace BunkFy.Modules.Workspaces.Application.Contributors;

internal static class WorkspaceStaffOnboardingRetentionCoordinates
{
    public const string OwnerKey = "workspaces";
    public const string DataClassKey = "staff-onboarding-staging";
    public const int ExecutionPolicyVersion = 1;

    public const string CompletedOutcome =
        "workspaces.staff-onboarding-staging.completed";
    public const string BacklogOutcome =
        "workspaces.staff-onboarding-staging.backlog";
    public const string AuthorityLapsedOutcome =
        "workspaces.staff-onboarding-staging.authority-lapsed";
    public const string ReconciliationFailedOutcome =
        "workspaces.staff-onboarding-staging.reconciliation-failed";
    public const string CoordinateInvalidOutcome =
        "workspaces.staff-onboarding-staging.coordinate-invalid";
}
