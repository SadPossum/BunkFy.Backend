namespace BunkFy.Modules.Workspaces.Application;

using Gma.Modules.Auth.Contracts;

public sealed class WorkspaceStaffOnboardingOptions
{
    public string GlobalAuthScopeId { get; set; } = AuthProfile.DefaultGlobalScopeId;
}
