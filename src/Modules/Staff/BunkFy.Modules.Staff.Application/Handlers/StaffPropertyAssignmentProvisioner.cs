namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class StaffPropertyAssignmentProvisioner(IRequestDispatcher dispatcher)
    : IStaffPropertyAssignmentProvisioner
{
    public async Task<StaffPropertyAssignmentProvisioningResult> ReconcileAsync(
        StaffPropertyAssignmentProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<IReadOnlyCollection<Guid>> result = await dispatcher.SendAsync(
            new ReconcileStaffPropertyAssignmentsCommand(
                request.StaffMemberId,
                request.PropertyIds,
                request.ActorId,
                request.Reason),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? new StaffPropertyAssignmentProvisioningResult(true, result.Value, null)
            : new StaffPropertyAssignmentProvisioningResult(false, [], result.Error.Code);
    }
}
