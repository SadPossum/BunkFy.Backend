namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class DenyWorkspaceStaffAccessCommandHandler(
    WorkspaceStaffAccessMutationCoordinator mutations,
    WorkspaceStaffAccessDenier denier)
    : ICommandHandler<DenyWorkspaceStaffAccessCommand, WorkspaceStaffAccessCoordinationOutcome>
{
    public async Task<Result<WorkspaceStaffAccessCoordinationOutcome>> HandleAsync(
        DenyWorkspaceStaffAccessCommand command,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessProcess? process = await mutations.AcquireExistingAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<WorkspaceStaffAccessCoordinationOutcome>(
                WorkspaceStaffAccessApplicationErrors.ProcessNotFound);
        }

        return Result.Success(await denier.DenyAsync(process, cancellationToken).ConfigureAwait(false));
    }
}
