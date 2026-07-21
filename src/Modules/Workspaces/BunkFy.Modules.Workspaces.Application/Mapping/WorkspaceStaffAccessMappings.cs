namespace BunkFy.Modules.Workspaces.Application.Mapping;

using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;

public static class WorkspaceStaffAccessMappings
{
    public static WorkspaceStaffAccessProcessDto ToDto(this WorkspaceStaffAccessProcess process) => new(
        process.Id,
        Guid.Parse(process.ScopeId),
        process.StaffMemberId,
        process.TargetState switch
        {
            WorkspaceStaffAccessTargetState.Active => WorkspaceStaffAccessTargetStatus.Active,
            WorkspaceStaffAccessTargetState.Suspended => WorkspaceStaffAccessTargetStatus.Suspended,
            WorkspaceStaffAccessTargetState.Departed => WorkspaceStaffAccessTargetStatus.Departed,
            _ => WorkspaceStaffAccessTargetStatus.Unknown
        },
        process.TargetStaffVersion,
        process.EffectiveOn,
        process.State switch
        {
            WorkspaceStaffAccessProcessState.Prepared => WorkspaceStaffAccessProcessStatus.Prepared,
            WorkspaceStaffAccessProcessState.AwaitingStaffCommit =>
                WorkspaceStaffAccessProcessStatus.AwaitingStaffCommit,
            WorkspaceStaffAccessProcessState.RestorationPending =>
                WorkspaceStaffAccessProcessStatus.RestorationPending,
            WorkspaceStaffAccessProcessState.Completed => WorkspaceStaffAccessProcessStatus.Completed,
            _ => WorkspaceStaffAccessProcessStatus.Unknown
        },
        process.ProfileSnapshots.Count,
        process.FailureCode,
        process.Version,
        process.CreatedAtUtc,
        process.LastChangedAtUtc,
        process.CompletedAtUtc);
}
