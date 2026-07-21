namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffLifecyclePolicy
{
    ValueTask<StaffLifecyclePolicyDecision> PrepareAsync(
        StaffLifecyclePolicyContext context,
        CancellationToken cancellationToken = default);
}
