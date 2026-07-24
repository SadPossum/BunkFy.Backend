namespace BunkFy.Modules.Guests.Contracts;

public enum GuestProcessingRestrictionActionDto
{
    Unknown = 0,
    Apply = 1,
    Release = 2
}

public sealed record GuestProcessingRestrictionReceiptDto(
    Guid ReceiptId,
    Guid RestrictionId,
    GuestProcessingRestrictionActionDto Action,
    Guid PropertyId,
    Guid GuestId,
    Guid CaseId,
    long ApprovalRevision,
    long SelectedGuestVersion,
    long RestrictionVersion,
    long ProjectionRevision,
    bool EffectiveRestricted,
    string ActorId,
    Guid EventId,
    DateTimeOffset CompletedAtUtc);

public sealed record GuestProcessingRestrictionDto(
    Guid RestrictionId,
    Guid GuestId,
    Guid ApplyCaseId,
    long ApplyApprovalRevision,
    long SelectedGuestVersion,
    long Version,
    string AppliedBy,
    DateTimeOffset AppliedAtUtc);

public sealed record GuestProcessingRestrictionListResponse(
    IReadOnlyCollection<GuestProcessingRestrictionDto> Restrictions,
    int Page,
    int PageSize);
