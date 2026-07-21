namespace BunkFy.Modules.Staff.Contracts;

public enum StaffLifecyclePolicyDecision
{
    Unknown = 0,
    Allowed = 1,
    Denied = 2,
    OwnerProtected = 3,
    RetryRequired = 4
}
