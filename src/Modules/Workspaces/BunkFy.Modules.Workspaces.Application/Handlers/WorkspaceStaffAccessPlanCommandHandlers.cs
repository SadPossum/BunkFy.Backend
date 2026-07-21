namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;

internal sealed class PrepareWorkspaceStaffAccessPlanCommandHandler(
    IWorkspaceStaffAccessPlanRepository plans,
    WorkspaceStaffAccessPlanPolicy policy,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<PrepareWorkspaceStaffAccessPlanCommand, WorkspaceStaffAccessPlanDto>
{
    public async Task<Result<WorkspaceStaffAccessPlanDto>> HandleAsync(
        PrepareWorkspaceStaffAccessPlanCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<WorkspaceStaffAccessPlanDto>(
                WorkspaceStaffOnboardingApplicationErrors.ScopeRequired);
        }

        WorkspaceStaffOnboardingSource sourceKind = command.SourceKind.ToDomain();
        Result<AccessProfileDto> profile = await policy.ValidateAsync(
                scopeContext.ScopeId,
                sourceKind,
                command.ProfileKey,
                command.PropertyIds,
                command.ActorSubjectId,
                cancellationToken)
            .ConfigureAwait(false);
        if (profile.IsFailure)
        {
            return Result.Failure<WorkspaceStaffAccessPlanDto>(profile.Error);
        }

        WorkspaceStaffAccessPlan? existing = await plans.GetAsync(
            command.SourceId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Matches(
                    sourceKind,
                    profile.Value.Id,
                    profile.Value.Key,
                    command.PropertyIds,
                    command.ActorSubjectId)
                ? Result.Success(existing.ToDto())
                : Result.Failure<WorkspaceStaffAccessPlanDto>(
                    WorkspaceStaffAccessPlanErrors.Conflict);
        }

        Result<WorkspaceStaffAccessPlan> created = WorkspaceStaffAccessPlan.Create(
            command.SourceId,
            scopeContext.ScopeId,
            sourceKind,
            profile.Value.Id,
            profile.Value.Key,
            command.PropertyIds,
            command.ActorSubjectId,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<WorkspaceStaffAccessPlanDto>(created.Error);
        }

        await plans.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToDto());
    }
}

internal sealed class ActivateWorkspaceStaffAccessPlanCommandHandler(
    IWorkspaceStaffAccessPlanRepository plans,
    ISystemClock clock)
    : ICommandHandler<ActivateWorkspaceStaffAccessPlanCommand, WorkspaceStaffAccessPlanDto>
{
    public async Task<Result<WorkspaceStaffAccessPlanDto>> HandleAsync(
        ActivateWorkspaceStaffAccessPlanCommand command,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessPlan? plan = await plans.GetAsync(
            command.SourceId,
            cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure<WorkspaceStaffAccessPlanDto>(
                WorkspaceStaffAccessPlanApplicationErrors.PlanNotFound);
        }

        Result activated = plan.Activate(clock.UtcNow);
        return activated.IsSuccess
            ? Result.Success(plan.ToDto())
            : Result.Failure<WorkspaceStaffAccessPlanDto>(activated.Error);
    }
}
