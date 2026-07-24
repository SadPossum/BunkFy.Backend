namespace BunkFy.Modules.DataRights.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Application.Authorization;
using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddDataRightsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IDataRightsOperationApprovalGate, DataRightsOperationApprovalGate>();
        services.AddGmaAccessControlPermissionPolicies(DataRightsModuleMetadata.Descriptor);
        return services;
    }
}
