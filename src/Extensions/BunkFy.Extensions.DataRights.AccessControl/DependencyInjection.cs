namespace BunkFy.Extensions.DataRights.AccessControl;

using BunkFy.Modules.DataRights.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddBunkFyAccessControlDataRights(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        AccessControlTenantTerminationExportSchema.EnsureValid();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                ITenantTerminationContributor,
                AccessControlTenantTerminationContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                ITenantTerminationExportContributor,
                AccessControlTenantTerminationContributor>());
        return services;
    }
}
