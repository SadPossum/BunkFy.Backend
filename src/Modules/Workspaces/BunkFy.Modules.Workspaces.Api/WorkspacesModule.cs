namespace BunkFy.Modules.Workspaces.Api;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.ModuleComposition;
using Gma.Modules.Auth.Contracts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class WorkspacesModule : IModule
{
    public string Name => WorkspacesModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(WorkspacesProfiles.Default, "BunkFy.Modules.Workspaces.Api");
        string globalAuthScopeId = builder.Configuration["Auth:GlobalScopeId"] ??
            AuthProfile.DefaultGlobalScopeId;
        builder.Services.AddWorkspacesApplication(
            builder.Configuration,
            globalAuthScopeId);
        builder.AddWorkspacesPersistence();
        builder.Services.AddOptions<WorkspacesApiSecurityOptions>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<
            ITenantEndpointAccessPolicy,
            WorkspaceTerminationEndpointAccessPolicy>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        WorkspacesApiSecurityOptions security = endpoints.ServiceProvider
            .GetRequiredService<IOptions<WorkspacesApiSecurityOptions>>()
            .Value;
        WorkspaceAccessManagementEndpoints.Map(endpoints, this.Name);
        WorkspaceStaffOnboardingEndpoints.Map(
            endpoints,
            this.Name,
            security);
        WorkspaceStaffOnboardingDataRightsEndpoints.Map(
            endpoints,
            this.Name);
    }
}
