namespace BunkFy.Modules.Workspaces.Application;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Microsoft.Extensions.DependencyInjection;

public interface IWorkspaceStaffOnboardingSubmitter
{
    Task<Result<WorkspaceStaffOnboardingDto>> SubmitAsync(
        SubmitWorkspaceStaffOnboardingCommand command,
        CancellationToken cancellationToken = default);
}

internal sealed class WorkspaceStaffOnboardingSubmitter(
    WorkspaceStaffJoinTokenAuthorityResolver authorityResolver,
    IWorkspaceAuthoritativeScope authoritativeScope)
    : IWorkspaceStaffOnboardingSubmitter
{
    public async Task<Result<WorkspaceStaffOnboardingDto>> SubmitAsync(
        SubmitWorkspaceStaffOnboardingCommand command,
        CancellationToken cancellationToken = default)
    {
        WorkspaceStaffJoinTokenAuthority? authority = await authorityResolver.ResolveAsync(
            command.SourceKind,
            command.Token,
            cancellationToken).ConfigureAwait(false);
        if (!authority.HasValue)
        {
            return Result.Failure<WorkspaceStaffOnboardingDto>(
                WorkspaceStaffOnboardingApplicationErrors.JoinTokenInvalid);
        }

        return await authoritativeScope.RunAsync(
            authority.Value.OrganizationId,
            async services => await services
                .GetRequiredService<IRequestDispatcher>()
                .SendAsync(command, cancellationToken)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }
}
