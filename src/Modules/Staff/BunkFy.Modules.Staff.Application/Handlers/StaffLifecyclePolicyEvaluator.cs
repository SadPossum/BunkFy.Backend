namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Results;

internal sealed class StaffLifecyclePolicyEvaluator(IEnumerable<IStaffLifecyclePolicy> policies)
{
    public async Task<Result> PrepareAsync(
        StaffLifecyclePolicyContext context,
        CancellationToken cancellationToken)
    {
        foreach (IStaffLifecyclePolicy policy in policies)
        {
            StaffLifecyclePolicyDecision decision = await policy
                .PrepareAsync(context, cancellationToken)
                .ConfigureAwait(false);
            Error? error = decision switch
            {
                StaffLifecyclePolicyDecision.Allowed => null,
                StaffLifecyclePolicyDecision.OwnerProtected =>
                    StaffApplicationErrors.WorkspaceOwnerProtected,
                StaffLifecyclePolicyDecision.RetryRequired =>
                    StaffApplicationErrors.LifecycleCoordinationPending,
                _ => StaffApplicationErrors.LifecycleTransitionDenied
            };
            if (error is not null)
            {
                return Result.Failure(error);
            }
        }

        return Result.Success();
    }
}
