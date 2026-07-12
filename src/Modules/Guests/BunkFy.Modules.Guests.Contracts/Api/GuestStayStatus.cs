namespace BunkFy.Modules.Guests.Contracts;

public enum GuestStayStatus
{
    Unknown = 0,
    PendingAllocation = 1,
    Confirmed = 2,
    AllocationRejected = 3,
    CancellationPending = 4,
    Cancelled = 5,
    CheckedIn = 6,
    NoShowPending = 7,
    NoShow = 8,
    CheckoutPending = 9,
    CheckedOut = 10
}
