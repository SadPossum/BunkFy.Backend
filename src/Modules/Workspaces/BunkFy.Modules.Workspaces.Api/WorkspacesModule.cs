namespace BunkFy.Modules.Workspaces.Api;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Api.Modules;
using Gma.Framework.ModuleComposition;
using Gma.Modules.Auth.Contracts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

public sealed class WorkspacesModule : IModule
{
    public string Name => WorkspacesModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(WorkspacesProfiles.Default, "BunkFy.Modules.Workspaces.Api");
        string globalAuthScopeId = builder.Configuration["Auth:GlobalScopeId"] ??
            AuthProfile.DefaultGlobalScopeId;
        builder.Services.AddWorkspacesApplication(globalAuthScopeId);
        builder.AddWorkspacesPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        WorkspaceAccessManagementEndpoints.Map(endpoints, this.Name);
        WorkspaceStaffOnboardingEndpoints.Map(endpoints, this.Name);
    }
}
