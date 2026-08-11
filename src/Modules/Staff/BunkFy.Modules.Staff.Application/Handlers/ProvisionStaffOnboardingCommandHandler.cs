namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class ProvisionStaffOnboardingCommandHandler(
    StaffOnboardingProvisioningCoordinator coordinator,
    IScopeContext scopeContext)
    : ICommandHandler<ProvisionStaffOnboardingCommand, StaffMemberDto>
{
    public Task<Result<StaffMemberDto>> HandleAsync(
        ProvisionStaffOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Task.FromResult(Result.Failure<StaffMemberDto>(
                StaffApplicationErrors.TenantRequired));
        }

        return coordinator.ExecuteAsync(
            command,
            scopeContext.ScopeId,
            cancellationToken);
    }
}
