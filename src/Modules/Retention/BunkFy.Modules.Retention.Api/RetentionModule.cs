namespace BunkFy.Modules.Retention.Api;

using BunkFy.Modules.Retention.Application;
using BunkFy.Modules.Retention.Application.Queries;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Persistence;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

public sealed class RetentionModule : IModule
{
    public string Name => RetentionModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(
            RetentionProfiles.Default,
            "BunkFy.Modules.Retention.Api");
        builder.Services.AddRetentionApplication();
        builder.AddRetentionPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/retention")
            .WithModuleName(this.Name)
            .WithTags("Retention")
            .RequireAuthorization();
        group.AddEndpointFilter(SensitiveResponseFilter);

        group.MapGet("/schedules", async (
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListRetentionScheduleHealthQuery(
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult())
            .Produces<RetentionScheduleHealthListResponse>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireTenantPermission(RetentionPermissionCodes.Read);
    }

    private static async ValueTask<object?> SensitiveResponseFilter(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        MarkSensitiveResponse(context.HttpContext);
        return await next(context).ConfigureAwait(false);
    }

    private static void MarkSensitiveResponse(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }
}
