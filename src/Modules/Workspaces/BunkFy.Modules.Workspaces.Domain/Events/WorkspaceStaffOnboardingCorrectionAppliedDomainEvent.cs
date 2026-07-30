namespace BunkFy.Modules.Workspaces.Domain.Events;

using Gma.Framework.Domain;

public sealed record WorkspaceStaffOnboardingCorrectionAppliedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string ScopeId,
    Guid ExecutionId,
    Guid ReceiptId,
    Guid CaseId,
    long ApprovalRevision,
    Guid ApplicationId,
    long SelectedRecordVersion,
    long CurrentRecordVersion,
    IReadOnlyCollection<WorkspaceStaffOnboardingApplicantField> ChangedFields)
    : ScopedDomainEvent(EventId, OccurredAtUtc, ScopeId);
