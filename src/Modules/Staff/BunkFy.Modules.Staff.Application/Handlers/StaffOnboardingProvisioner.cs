namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class StaffOnboardingProvisioner(IRequestDispatcher dispatcher)
    : IStaffOnboardingProvisioner
{
    public async Task<StaffOnboardingProvisioningResult> ProvisionAsync(
        StaffOnboardingProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<StaffMemberDto> result = await dispatcher.SendAsync(
            new ProvisionStaffOnboardingCommand(
                request.AuthSubjectId,
                request.DisplayName,
                request.LegalName,
                request.WorkEmail,
                request.WorkPhone,
                request.EmployeeNumber,
                request.JobTitle,
                request.Department,
                request.ActorId,
                request.Reason),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? new StaffOnboardingProvisioningResult(true, result.Value.StaffMemberId, null)
            : new StaffOnboardingProvisioningResult(false, null, result.Error.Code);
    }
}
