namespace BunkFy.Modules.Staff.Contracts;

public enum StaffProcessingRestrictionActionDto
{
    Unknown = 0,
    Apply = 1,
    Release = 2
}

public sealed record StaffProcessingRestrictionReceiptDto(
    Guid ReceiptId,
    Guid RestrictionId,
    StaffProcessingRestrictionActionDto Action,
    Guid StaffMemberId,
    Guid CaseId,
    long ApprovalRevision,
    long SelectedStaffVersion,
    long RestrictionVersion,
    long ProjectionRevision,
    bool EffectiveRestricted,
    string ActorId,
    Guid EventId,
    DateTimeOffset CompletedAtUtc);
