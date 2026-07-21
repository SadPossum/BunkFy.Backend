namespace BunkFy.Modules.Workspaces.Application.Mapping;

using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;

internal static class WorkspaceStaffAccessPlanMappings
{
    public static WorkspaceStaffAccessPlanDto ToDto(this WorkspaceStaffAccessPlan plan) => new(
        plan.Id,
        plan.SourceKind.ToContract(),
        plan.ProfileId,
        plan.ProfileKey,
        plan.Properties.Select(property => property.PropertyId).Order().ToArray(),
        plan.Status switch
        {
            WorkspaceStaffAccessPlanState.Prepared => WorkspaceStaffAccessPlanStatus.Prepared,
            WorkspaceStaffAccessPlanState.Active => WorkspaceStaffAccessPlanStatus.Active,
            WorkspaceStaffAccessPlanState.Superseded => WorkspaceStaffAccessPlanStatus.Superseded,
            _ => WorkspaceStaffAccessPlanStatus.Unknown
        },
        plan.Version,
        plan.CreatedAtUtc,
        plan.LastChangedAtUtc);
}
