namespace BunkFy.Modules.Guests.Application.Commands;

internal enum GuestRetentionMutationStatus
{
    Applied = 1,
    AlreadyApplied = 2,
    NoLongerEligible = 3,
    Blocked = 4,
    Failed = 5
}
