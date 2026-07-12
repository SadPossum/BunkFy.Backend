namespace BunkFy.Modules.Properties.Application;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Application.Composition;
using Microsoft.Extensions.DependencyInjection;
public static class DependencyInjection
{
    public static IServiceCollection AddPropertiesApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGmaAccessControlPermissionPolicies(PropertiesModuleMetadata.Descriptor);
        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
