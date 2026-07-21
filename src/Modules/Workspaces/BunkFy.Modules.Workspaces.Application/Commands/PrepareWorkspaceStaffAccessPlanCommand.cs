namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

public sealed record PrepareWorkspaceStaffAccessPlanCommand(
    Guid SourceId,
    WorkspaceStaffOnboardingSourceKind SourceKind,
    string ProfileKey,
    IReadOnlyCollection<Guid> PropertyIds,
    string ActorSubjectId) : ITransactionalCommand<WorkspaceStaffAccessPlanDto>;

public sealed record ActivateWorkspaceStaffAccessPlanCommand(Guid SourceId)
    : ITransactionalCommand<WorkspaceStaffAccessPlanDto>;
