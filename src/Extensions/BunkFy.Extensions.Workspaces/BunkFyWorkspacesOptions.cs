namespace BunkFy.Extensions.Workspaces;

using Gma.Modules.Auth.Contracts;

public sealed class BunkFyWorkspacesOptions
{
    public string GlobalAuthScopeId { get; set; } = AuthProfile.DefaultGlobalScopeId;
}
