namespace BunkFy.Modules.Staff.Api;

using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Modules;
using Gma.Framework.ModuleComposition;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Persistence;

public sealed class StaffModule : IModule
{
    public string Name => StaffModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(StaffProfiles.Default, "BunkFy.Modules.Staff.Api");
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAccessHttpScopeResolver, StaffPropertyAccessScopeResolver>());
        builder.Services.AddStaffApplication();
        builder.AddStaffPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        StaffSelfServiceEndpoints.Map(endpoints, this.Name);
        StaffMemberEndpoints.Map(endpoints, this.Name);
        StaffPropertyAssignmentEndpoints.Map(endpoints, this.Name);
    }
}
