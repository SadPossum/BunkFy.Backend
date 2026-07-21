namespace BunkFy.Modules.Workspaces.Application.Queries;

using Gma.Framework.Cqrs;

public sealed record GetWorkspaceAccessBootstrapStatusQuery : IQuery<WorkspaceAccessBootstrapStatus>;
