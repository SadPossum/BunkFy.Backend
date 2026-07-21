namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

public sealed record RetryWorkspaceStaffAccessProcessCommand(Guid ProcessId)
    : ITransactionalCommand<WorkspaceStaffAccessProcessDto>;
