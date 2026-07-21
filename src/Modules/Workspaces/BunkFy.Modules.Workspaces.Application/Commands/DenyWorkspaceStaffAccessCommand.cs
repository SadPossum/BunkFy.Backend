namespace BunkFy.Modules.Workspaces.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record DenyWorkspaceStaffAccessCommand(Guid ProcessId)
    : ITransactionalCommand<WorkspaceStaffAccessCoordinationOutcome>;

public enum WorkspaceStaffAccessCoordinationOutcome
{
    Unknown = 0,
    Allowed = 1,
    OwnerProtected = 2,
    RetryRequired = 3
}
