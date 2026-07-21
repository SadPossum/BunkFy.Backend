namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class RetryWorkspaceStaffOnboardingCommandHandler(
    IWorkspaceStaffOnboardingRepository applications,
    WorkspaceStaffOnboardingProcessor processor)
    : ICommandHandler<RetryWorkspaceStaffOnboardingCommand, WorkspaceStaffOnboardingDto>
{
    public async Task<Result<WorkspaceStaffOnboardingDto>> HandleAsync(
        RetryWorkspaceStaffOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffOnboarding? application = await applications.GetAsync(
            command.ApplicationId,
            cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result.Failure<WorkspaceStaffOnboardingDto>(
                WorkspaceStaffOnboardingApplicationErrors.ApplicationNotFound);
        }

        Result processed = await processor.ProcessAsync(application, cancellationToken).ConfigureAwait(false);
        return processed.IsSuccess
            ? Result.Success(application.ToDto())
            : Result.Failure<WorkspaceStaffOnboardingDto>(processed.Error);
    }
}
