namespace BunkFy.Adapters.Configuration;

using BunkFy.Modules.Ingestion.Contracts.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddLocalAdapterConfigurationMaterials(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddScoped<IAdapterConfigurationMaterialResolver>(_ =>
            new ConfigurationAdapterMaterialResolver(configuration));
        return services;
    }
}
