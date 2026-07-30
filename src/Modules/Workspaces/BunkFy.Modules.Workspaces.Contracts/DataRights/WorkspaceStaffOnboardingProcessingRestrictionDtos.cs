namespace BunkFy.Modules.Workspaces.Contracts;

public enum WorkspaceStaffOnboardingProcessingRestrictionActionDto
{
    Unknown = 0,
    Apply = 1,
    Release = 2
}

public sealed record WorkspaceStaffOnboardingProcessingRestrictionReceiptDto(
    Guid ReceiptId,
    Guid RestrictionId,
    WorkspaceStaffOnboardingProcessingRestrictionActionDto Action,
    Guid ApplicationId,
    Guid CaseId,
    long ApprovalRevision,
    long SelectedOnboardingVersion,
    long RestrictionVersion,
    long ProjectionRevision,
    bool EffectiveRestricted,
    string ActorId,
    Guid EventId,
    DateTimeOffset CompletedAtUtc);
