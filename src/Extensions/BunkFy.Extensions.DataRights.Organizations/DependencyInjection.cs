namespace BunkFy.Extensions.DataRights.Organizations;

using BunkFy.Modules.DataRights.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddBunkFyOrganizationsDataRights(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        OrganizationsTenantTerminationExportSchema.EnsureValid();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                ITenantTerminationContributor,
                OrganizationsTenantTerminationContributor>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                ITenantTerminationExportContributor,
                OrganizationsTenantTerminationContributor>());
        return services;
    }
}
