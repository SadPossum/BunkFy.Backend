namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;

internal sealed record WorkspaceStaffOnboardingDataRightsExport(
    Guid Id,
    string ScopeId,
    WorkspaceStaffOnboardingSource SourceKind,
    Guid SourceId,
    Guid? ClaimId,
    long? ClaimVersion,
    string SubjectId,
    string? VerifiedAccountEmail,
    string? DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department,
    WorkspaceStaffOnboardingState Status,
    Guid? StaffMemberId,
    string? FailureCode,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);

internal sealed record
    WorkspaceStaffOnboardingCorrectionReceiptDataRightsExport(
        int ContractVersion,
        Guid ReceiptId,
        Guid ExecutionId,
        Guid CaseId,
        long ApprovalRevision,
        Guid ApplicationId,
        long SelectedRecordVersion,
        long CurrentRecordVersion,
        IReadOnlyCollection<WorkspaceStaffOnboardingApplicantField>
            ChangedFields,
        Guid ApplicantEventId,
        Guid CompletionEventId,
        DateTimeOffset CompletedAtUtc);

internal sealed record
    WorkspaceStaffOnboardingProcessingRestrictionDataRightsExport(
        Guid RestrictionId,
        Guid ApplicationId,
        Guid ApplyCaseId,
        long ApplyApprovalRevision,
        long ApplySelectedOnboardingVersion,
        WorkspaceStaffOnboardingProcessingRestrictionState Status,
        long Version,
        DateTimeOffset AppliedAtUtc,
        Guid? ReleaseCaseId,
        long? ReleaseApprovalRevision,
        long? ReleaseSelectedOnboardingVersion,
        DateTimeOffset? ReleasedAtUtc);

internal sealed record
    WorkspaceStaffOnboardingProcessingRestrictionReceiptDataRightsExport(
        Guid ReceiptId,
        Guid RestrictionId,
        WorkspaceStaffOnboardingProcessingRestrictionAction Action,
        Guid ApplicationId,
        Guid CaseId,
        long ApprovalRevision,
        long SelectedOnboardingVersion,
        long ResultingRestrictionVersion,
        long ResultingProjectionRevision,
        bool EffectiveRestricted,
        Guid EventId,
        DateTimeOffset CompletedAtUtc);

internal sealed record WorkspaceStaffAccessProcessDataRightsExport(
    Guid Id,
    string ScopeId,
    Guid StaffMemberId,
    string SubjectId,
    WorkspaceStaffAccessTargetState TargetState,
    long TargetStaffVersion,
    DateOnly EffectiveOn,
    string RequestedBy,
    WorkspaceStaffAccessProcessState State,
    string? FailureCode,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc,
    DateTimeOffset? CompletedAtUtc);

internal sealed record WorkspaceStaffAccessProfileDataRightsExport(
    Guid ProcessId,
    Guid ProfileId,
    string AssignmentScope);

internal sealed record WorkspaceStaffAccessPlanDataRightsExport(
    Guid Id,
    string ScopeId,
    WorkspaceStaffOnboardingSource SourceKind,
    Guid ProfileId,
    string ProfileKey,
    string CreatedBySubjectId,
    WorkspaceStaffAccessPlanState Status,
    DateTimeOffset? SourceExpiredAtUtc,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);

internal sealed record WorkspaceStaffAccessPlanPropertyDataRightsExport(
    string ScopeId,
    Guid PlanId,
    Guid PropertyId);

internal sealed record WorkspaceStaffRetentionCorrelationDataRightsExport(
    string ScopeId,
    Guid StaffMemberId,
    long SelectedStaffVersion,
    WorkspaceStaffRetentionCorrelationProofDataRightsExport Proof);

internal sealed record WorkspaceStaffRetentionCorrelationProofDataRightsExport(
    Guid Id,
    int ContractVersion,
    Guid ExecutionId,
    int OnboardingRecordsScrubbed,
    int AccessProcessRecordsScrubbed,
    int AccessPlanRecordsScrubbed,
    DateTimeOffset CompletedAtUtc,
    string CanonicalSha256);
