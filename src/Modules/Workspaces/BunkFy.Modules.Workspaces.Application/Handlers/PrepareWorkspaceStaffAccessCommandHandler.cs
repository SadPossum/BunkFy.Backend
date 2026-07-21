namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class PrepareWorkspaceStaffAccessCommandHandler(
    IWorkspaceStaffAccessProcessRepository processes,
    WorkspaceAccessProvisioner access,
    ISystemClock clock)
    : ICommandHandler<PrepareWorkspaceStaffAccessCommand, WorkspaceStaffAccessPreparation>
{
    public async Task<Result<WorkspaceStaffAccessPreparation>> HandleAsync(
        PrepareWorkspaceStaffAccessCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command.Context);
        StaffLifecyclePolicyContext context = command.Context;
        if (string.IsNullOrWhiteSpace(context.AuthSubjectId))
        {
            return Result.Failure<WorkspaceStaffAccessPreparation>(
                WorkspaceStaffAccessApplicationErrors.ProcessConflict);
        }

        WorkspaceStaffAccessTargetState targetState = ToTargetState(context.TargetStatus);
        WorkspaceStaffAccessProcess? replay = await processes.GetByStaffVersionAsync(
            context.StaffMemberId,
            context.TargetVersion,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Matches(context.AuthSubjectId, targetState, context.EffectiveOn)
                ? Result.Success(ToPreparation(replay))
                : Result.Failure<WorkspaceStaffAccessPreparation>(
                    WorkspaceStaffAccessApplicationErrors.ProcessConflict);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        WorkspaceStaffAccessProcess? observedPriorCommit = null;
        WorkspaceStaffAccessProcess? open = await processes.GetOpenByStaffAsync(
            context.StaffMemberId,
            cancellationToken).ConfigureAwait(false);
        if (open is not null)
        {
            WorkspaceStaffAccessTargetState previousState = ToTargetState(context.PreviousStatus);
            bool provesPriorCommit = open.State == WorkspaceStaffAccessProcessState.AwaitingStaffCommit &&
                open.TargetState != WorkspaceStaffAccessTargetState.Active &&
                open.TargetState == previousState &&
                open.TargetStaffVersion == context.ExpectedVersion;
            if (!provesPriorCommit || open.ObserveStaffCommit(nowUtc).IsFailure)
            {
                return Result.Failure<WorkspaceStaffAccessPreparation>(
                    WorkspaceStaffAccessApplicationErrors.ProcessConflict);
            }

            observedPriorCommit = open;
        }

        IReadOnlyCollection<WorkspaceStaffAccessProfileTarget> profileTargets;
        if (targetState == WorkspaceStaffAccessTargetState.Active)
        {
            WorkspaceStaffAccessProcess? suspension = observedPriorCommit ??
                await processes.GetLatestCompletedSuspensionAsync(
                        context.StaffMemberId,
                        context.AuthSubjectId,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (suspension is null)
            {
                return Result.Failure<WorkspaceStaffAccessPreparation>(
                    WorkspaceStaffAccessApplicationErrors.ResumeSnapshotUnavailable);
            }

            profileTargets = suspension.ProfileSnapshots
                .Select(snapshot => new WorkspaceStaffAccessProfileTarget(
                    snapshot.ProfileId,
                    snapshot.AssignmentScope))
                .ToArray();
        }
        else
        {
            profileTargets = await access.CaptureRestorableProfilesAsync(
                context.ScopeId,
                context.AuthSubjectId,
                cancellationToken).ConfigureAwait(false);
        }

        Result<WorkspaceStaffAccessProcess> created = WorkspaceStaffAccessProcess.Create(
            context.TransitionId,
            context.ScopeId,
            context.StaffMemberId,
            context.AuthSubjectId,
            targetState,
            context.TargetVersion,
            context.EffectiveOn,
            context.ActorId,
            profileTargets,
            nowUtc);
        if (created.IsFailure)
        {
            return Result.Failure<WorkspaceStaffAccessPreparation>(created.Error);
        }

        if (targetState == WorkspaceStaffAccessTargetState.Active)
        {
            Result awaiting = created.Value.MarkAwaitingStaffCommit(nowUtc);
            if (awaiting.IsFailure)
            {
                return Result.Failure<WorkspaceStaffAccessPreparation>(awaiting.Error);
            }
        }

        await processes.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(ToPreparation(created.Value));
    }

    private static WorkspaceStaffAccessPreparation ToPreparation(WorkspaceStaffAccessProcess process) =>
        new(process.Id, process.State == WorkspaceStaffAccessProcessState.Prepared);

    private static WorkspaceStaffAccessTargetState ToTargetState(StaffStatus status) => status switch
    {
        StaffStatus.Active => WorkspaceStaffAccessTargetState.Active,
        StaffStatus.Suspended => WorkspaceStaffAccessTargetState.Suspended,
        StaffStatus.Departed => WorkspaceStaffAccessTargetState.Departed,
        _ => WorkspaceStaffAccessTargetState.Unknown
    };
}
