namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class RetryWorkspaceStaffAccessProcessCommandHandler(
    IWorkspaceStaffAccessProcessRepository processes,
    WorkspaceStaffAccessDenier denier,
    WorkspaceStaffAccessRestorer restorer)
    : ICommandHandler<RetryWorkspaceStaffAccessProcessCommand, WorkspaceStaffAccessProcessDto>
{
    public async Task<Result<WorkspaceStaffAccessProcessDto>> HandleAsync(
        RetryWorkspaceStaffAccessProcessCommand command,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessProcess? process = await processes.GetAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<WorkspaceStaffAccessProcessDto>(
                WorkspaceStaffAccessApplicationErrors.ProcessNotFound);
        }

        WorkspaceStaffAccessCoordinationOutcome outcome = process.State switch
        {
            WorkspaceStaffAccessProcessState.Prepared =>
                await denier.DenyAsync(process, cancellationToken).ConfigureAwait(false),
            WorkspaceStaffAccessProcessState.RestorationPending =>
                await restorer.RestoreAsync(process, cancellationToken).ConfigureAwait(false),
            WorkspaceStaffAccessProcessState.Completed => WorkspaceStaffAccessCoordinationOutcome.Allowed,
            _ => WorkspaceStaffAccessCoordinationOutcome.RetryRequired
        };
        return outcome switch
        {
            WorkspaceStaffAccessCoordinationOutcome.Allowed => Result.Success(process.ToDto()),
            WorkspaceStaffAccessCoordinationOutcome.OwnerProtected =>
                Result.Failure<WorkspaceStaffAccessProcessDto>(
                    WorkspaceStaffAccessApplicationErrors.OwnerProtected),
            _ => Result.Failure<WorkspaceStaffAccessProcessDto>(
                WorkspaceStaffAccessApplicationErrors.RetryPending)
        };
    }
}
