namespace BunkFy.Modules.DataRights.AdminApi;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
public sealed class DataRightsAdminApiModule : IAdminApiModule
{
    public string Name => DataRightsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddDataRightsApplication();
        builder.AddDataRightsPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapGroup("/api/admin/" + DataRightsModuleMetadata.Name)
            .WithModuleName(this.Name)
            .WithTags("DataRights Admin")
            .RequireAuthorization();
}
