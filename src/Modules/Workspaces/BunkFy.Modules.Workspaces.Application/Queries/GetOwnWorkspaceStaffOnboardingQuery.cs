namespace BunkFy.Modules.Workspaces.Application.Queries;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetOwnWorkspaceStaffOnboardingQuery(
    WorkspaceStaffOnboardingSourceKind SourceKind,
    Guid SourceId,
    string SubjectId) : IQuery<WorkspaceStaffOnboardingDto>;
