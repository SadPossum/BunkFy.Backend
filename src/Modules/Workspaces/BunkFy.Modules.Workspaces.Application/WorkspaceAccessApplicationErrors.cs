namespace BunkFy.Modules.Workspaces.Application;

using Gma.Framework.Results;

public static class WorkspaceAccessApplicationErrors
{
    public static readonly Error ScopeRequired = new(
        "Workspaces.AccessScopeRequired",
        "A workspace scope is required.");

    public static readonly Error BootstrapFailed = new(
        "Workspaces.AccessBootstrapFailed",
        "Workspace access bootstrap did not complete.");
}
