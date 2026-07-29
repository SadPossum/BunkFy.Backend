namespace BunkFy.Modules.Reservations.Application.Commands;

internal enum ReservationRetentionMutationStatus
{
    Applied = 1,
    AlreadyApplied = 2,
    NoLongerEligible = 3,
    Blocked = 4,
    Failed = 5
}
