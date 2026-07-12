namespace BunkFy.Adapters.JsonFileDrop;

using BunkFy.Adapter.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddJsonFileDropAdapter(
        this IServiceCollection services,
        JsonFileDropAdapterOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddJsonFileDropAdapterDescriptor();
        services.TryAddSingleton(options);
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAdapterRunner, JsonFileDropAdapterRunner>());
        return services;
    }

    public static IServiceCollection AddJsonFileDropAdapterDescriptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAdapterDescriptorProvider, JsonFileDropAdapterDescriptor>());
        return services;
    }
}
