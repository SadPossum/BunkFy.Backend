namespace BunkFy.Modules.Reservations.Domain.GuestRecords;

public enum ReservationGuestRecordLinkProcessState
{
    Unknown = 0,
    Prepared = 1,
    Ready = 2,
    Completed = 3,
    NeedsReview = 4
}
