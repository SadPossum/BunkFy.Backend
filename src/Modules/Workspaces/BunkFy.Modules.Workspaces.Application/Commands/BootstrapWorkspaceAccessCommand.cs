namespace BunkFy.Modules.Workspaces.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record BootstrapWorkspaceAccessCommand : ICommand<WorkspaceAccessBootstrapResult>;
