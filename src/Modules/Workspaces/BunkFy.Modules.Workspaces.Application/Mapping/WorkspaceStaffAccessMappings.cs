namespace BunkFy.Modules.Workspaces.Application.Mapping;

using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;

public static class WorkspaceStaffAccessMappings
{
    public static WorkspaceStaffAccessProcessDto ToDto(this WorkspaceStaffAccessProcess process) => new(
        process.Id,
        Guid.Parse(process.ScopeId),
        process.StaffMemberId,
        MapTargetStatus(process.TargetState),
        process.TargetStaffVersion,
        process.EffectiveOn,
        MapStatus(process.State),
        process.ProfileSnapshots.Count,
        process.FailureCode,
        process.Version,
        process.CreatedAtUtc,
        process.LastChangedAtUtc,
        process.CompletedAtUtc);

    public static WorkspaceStaffAccessTargetStatus MapTargetStatus(
        WorkspaceStaffAccessTargetState status) => status switch
        {
            WorkspaceStaffAccessTargetState.Active => WorkspaceStaffAccessTargetStatus.Active,
            WorkspaceStaffAccessTargetState.Suspended => WorkspaceStaffAccessTargetStatus.Suspended,
            WorkspaceStaffAccessTargetState.Departed => WorkspaceStaffAccessTargetStatus.Departed,
            _ => WorkspaceStaffAccessTargetStatus.Unknown
        };

    public static WorkspaceStaffAccessProcessStatus MapStatus(
        WorkspaceStaffAccessProcessState status) => status switch
        {
            WorkspaceStaffAccessProcessState.Prepared => WorkspaceStaffAccessProcessStatus.Prepared,
            WorkspaceStaffAccessProcessState.AwaitingStaffCommit =>
                WorkspaceStaffAccessProcessStatus.AwaitingStaffCommit,
            WorkspaceStaffAccessProcessState.RestorationPending =>
                WorkspaceStaffAccessProcessStatus.RestorationPending,
            WorkspaceStaffAccessProcessState.Completed => WorkspaceStaffAccessProcessStatus.Completed,
            _ => WorkspaceStaffAccessProcessStatus.Unknown
        };
}
