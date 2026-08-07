namespace BunkFy.Modules.Reservations.Domain.GuestRecords;

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
