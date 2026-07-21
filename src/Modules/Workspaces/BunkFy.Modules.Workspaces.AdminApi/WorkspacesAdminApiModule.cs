namespace BunkFy.Modules.Workspaces.AdminApi;

using BunkFy.Modules.Workspaces.Admin.Contracts;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Results;
using Gma.Modules.Auth.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

public sealed class WorkspacesAdminApiModule : IAdminApiModule
{
    public string Name => WorkspacesModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(WorkspacesProfiles.Default, "BunkFy.Modules.Workspaces.AdminApi");
        string globalAuthScopeId = builder.Configuration["Auth:GlobalScopeId"] ??
            AuthProfile.DefaultGlobalScopeId;
        builder.Services.AddWorkspacesApplication(globalAuthScopeId);
        builder.AddWorkspacesPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder access = endpoints.MapGroup("/api/admin/workspaces/access-bootstrap")
            .WithModuleName(this.Name)
            .WithTags("Workspaces Admin")
            .RequireAuthorization();

        access.MapGet("/", async (
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(
                    WorkspacesAdminOperationNames.AccessBootstrapStatus,
                    WorkspacesAdminPermissions.AccessBootstrap),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetWorkspaceAccessBootstrapStatusQuery(), token),
                cancellationToken).ConfigureAwait(false));

        access.MapPost("/", async (
            AccessBootstrapRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(
                    WorkspacesAdminOperationNames.AccessBootstrapRun,
                    WorkspacesAdminPermissions.AccessBootstrap),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new BootstrapWorkspaceAccessCommand(), token)
                    : Task.FromResult(Result.Failure<WorkspaceAccessBootstrapResult>(
                        AdminErrors.ConfirmationRequired)),
                cancellationToken).ConfigureAwait(false));
    }

    public sealed record AccessBootstrapRequest(bool Confirmed);
}
