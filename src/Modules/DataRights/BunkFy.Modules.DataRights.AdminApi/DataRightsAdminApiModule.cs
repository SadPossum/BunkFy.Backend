namespace BunkFy.Modules.DataRights.AdminApi;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Extensions.DataRights.TenantTermination;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.ModuleComposition;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class DataRightsAdminApiModule : IAdminApiModule
{
    public string Name => DataRightsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(
            DataRightsProfiles.Default,
            "BunkFy.Modules.DataRights.AdminApi");
        builder.Services.AddDataRightsApplication();
        builder.Services.AddDataRightsTenantTerminationTaskScheduling();
        builder.Services.AddBunkFyTenantTerminationOperatorCatalog();
        builder.AddDataRightsPersistence();
        builder.AddDataRightsRestoreReadinessGate();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
        => TenantTerminationAdminEndpoints.Map(endpoints, this.Name);
}
