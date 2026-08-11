namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationGuestRecordLinkPreparationDto(
    ReservationGuestRecordLinkProcessDto Process,
    Guid CreationConfirmationId,
    string? GuestCreationActorId);

public sealed record ReservationGuestRecordLinkProcessDto(
    Guid OperationId,
    Guid PropertyId,
    Guid ReservationId,
    Guid GuestId,
    ReservationGuestRecordLinkStatus Status,
    ReservationGuestRecordLinkReviewReason ReviewReason,
    long Revision,
    int DispatchRevision,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public bool IsTerminal => this.Status is
        ReservationGuestRecordLinkStatus.Completed or
        ReservationGuestRecordLinkStatus.NeedsReview;
}

public enum ReservationGuestRecordLinkStatus
{
    Unknown = 0,
    Prepared = 1,
    Ready = 2,
    Completed = 3,
    NeedsReview = 4
}

public enum ReservationGuestRecordLinkReviewReason
{
    Unknown = 0,
    None = 1,
    ReservationUnavailable = 2,
    PrimaryGuestOccupied = 3,
    GuestUnavailable = 4,
    GuestRestricted = 5,
    CountryPolicyDenied = 6,
    RetryLimitReached = 7
}
