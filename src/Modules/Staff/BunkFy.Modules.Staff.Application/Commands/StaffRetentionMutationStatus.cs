namespace BunkFy.Modules.Staff.Application.Commands;

internal enum StaffRetentionMutationStatus
{
    Applied = 1,
    AlreadyApplied = 2,
    NoLongerEligible = 3,
    Blocked = 4,
    Failed = 5
}
