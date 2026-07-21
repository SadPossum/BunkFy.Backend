namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Time;

[IntegrationEventHandler(HandlerName, RequiresExplicitProducerBinding = true)]
internal sealed class StaffLifecycleWorkspaceAccessHandler(
    IWorkspaceStaffAccessProcessRepository processes,
    WorkspaceStaffAccessRestorer restorer,
    ISystemClock clock)
    : IIntegrationEventHandler<StaffMemberLifecycleChangedIntegrationEvent>
{
    public const string HandlerName = WorkspacesModuleMetadata.StaffAccessLifecycleHandlerName;

    public async Task HandleAsync(
        StaffMemberLifecycleChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessProcess? process = await processes.GetByStaffVersionAsync(
            integrationEvent.StaffMemberId,
            integrationEvent.StaffVersion,
            cancellationToken).ConfigureAwait(false);
        if (process is null || process.State == WorkspaceStaffAccessProcessState.Completed)
        {
            return;
        }

        if (process.TargetState != ToTargetState(integrationEvent.Status))
        {
            throw new InvalidOperationException("The Staff lifecycle event does not match its access process.");
        }

        if (process.ObserveStaffCommit(clock.UtcNow).IsFailure)
        {
            throw new InvalidOperationException("The Staff lifecycle process could not observe the Staff commit.");
        }

        if (process.TargetState == WorkspaceStaffAccessTargetState.Active)
        {
            await restorer.RestoreAsync(process, cancellationToken).ConfigureAwait(false);
        }
    }

    private static WorkspaceStaffAccessTargetState ToTargetState(StaffStatus status) => status switch
    {
        StaffStatus.Active => WorkspaceStaffAccessTargetState.Active,
        StaffStatus.Suspended => WorkspaceStaffAccessTargetState.Suspended,
        StaffStatus.Departed => WorkspaceStaffAccessTargetState.Departed,
        _ => WorkspaceStaffAccessTargetState.Unknown
    };
}
