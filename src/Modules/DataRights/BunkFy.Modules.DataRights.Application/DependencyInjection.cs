namespace BunkFy.Modules.DataRights.Application;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddDataRightsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddGmaAccessControlPermissionPolicies(DataRightsModuleMetadata.Descriptor);
        return services;
    }
}
