namespace BunkFy.Modules.Workspaces.Application.Mapping;

using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;

public static class WorkspaceStaffOnboardingMappings
{
    public static WorkspaceStaffOnboardingSource ToDomain(
        this WorkspaceStaffOnboardingSourceKind sourceKind) => sourceKind switch
        {
            WorkspaceStaffOnboardingSourceKind.Invitation => WorkspaceStaffOnboardingSource.Invitation,
            WorkspaceStaffOnboardingSourceKind.EnrollmentLink => WorkspaceStaffOnboardingSource.EnrollmentLink,
            _ => WorkspaceStaffOnboardingSource.Unknown
        };

    public static WorkspaceStaffOnboardingSourceKind ToContract(
        this WorkspaceStaffOnboardingSource sourceKind) => sourceKind switch
        {
            WorkspaceStaffOnboardingSource.Invitation => WorkspaceStaffOnboardingSourceKind.Invitation,
            WorkspaceStaffOnboardingSource.EnrollmentLink => WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
            _ => WorkspaceStaffOnboardingSourceKind.Unknown
        };

    public static WorkspaceStaffOnboardingDto ToDto(this WorkspaceStaffOnboarding application) => new(
        application.Id,
        Guid.Parse(application.ScopeId),
        application.SourceKind.ToContract(),
        application.SourceId,
        application.ClaimId,
        application.ClaimVersion,
        application.SubjectId,
        application.VerifiedAccountEmail,
        application.DisplayName,
        application.LegalName,
        application.WorkEmail,
        application.WorkPhone,
        application.EmployeeNumber,
        application.JobTitle,
        application.Department,
        application.Status switch
        {
            WorkspaceStaffOnboardingState.Submitted => WorkspaceStaffOnboardingStatus.Submitted,
            WorkspaceStaffOnboardingState.PendingApproval => WorkspaceStaffOnboardingStatus.PendingApproval,
            WorkspaceStaffOnboardingState.Provisioning => WorkspaceStaffOnboardingStatus.Provisioning,
            WorkspaceStaffOnboardingState.StaffReady => WorkspaceStaffOnboardingStatus.StaffReady,
            WorkspaceStaffOnboardingState.Completed => WorkspaceStaffOnboardingStatus.Completed,
            WorkspaceStaffOnboardingState.Failed => WorkspaceStaffOnboardingStatus.Failed,
            WorkspaceStaffOnboardingState.Rejected => WorkspaceStaffOnboardingStatus.Rejected,
            WorkspaceStaffOnboardingState.Superseded => WorkspaceStaffOnboardingStatus.Superseded,
            _ => WorkspaceStaffOnboardingStatus.Unknown
        },
        application.StaffMemberId,
        application.Version,
        application.FailureCode,
        application.CreatedAtUtc,
        application.LastChangedAtUtc);
}
