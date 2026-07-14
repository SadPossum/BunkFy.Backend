namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class StaffIdentityReconciler(IRequestDispatcher dispatcher)
    : IStaffIdentityReconciler
{
    public async Task<StaffIdentityReconciliationResult> ReconcileAsync(
        StaffIdentityReconciliationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<Unit> result = await dispatcher.SendAsync(
            new ReconcileStaffIdentityCommand(
                request.AuthSubjectId,
                request.DisplayName,
                request.WorkEmail,
                request.IsActive,
                request.ActorId,
                request.Reason),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? new StaffIdentityReconciliationResult(true, null)
            : new StaffIdentityReconciliationResult(false, result.Error.Code);
    }
}
