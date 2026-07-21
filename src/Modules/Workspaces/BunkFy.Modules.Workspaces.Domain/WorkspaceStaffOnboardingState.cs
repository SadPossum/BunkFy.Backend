namespace BunkFy.Modules.Workspaces.Domain;

public enum WorkspaceStaffOnboardingState
{
    Unknown = 0,
    Submitted = 1,
    PendingApproval = 2,
    Provisioning = 3,
    StaffReady = 4,
    Completed = 5,
    Failed = 6,
    Rejected = 7,
    Superseded = 8
}
