namespace BunkFy.Modules.Stations.Application;

using BunkFy.Modules.Stations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddStationsCore(this IServiceCollection services, Action<StationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<StationOptions>().Configure(configure).Validate(o => o.IsValid(), "Invalid station core configuration.").ValidateOnStart();
        services.TryAddSingleton<IStationPinVerifier, StationPinVerifier>();
        return services;
    }

    /// <summary>Explicit module registration only; no host opts in or activates a route here.</summary>
    public static IServiceCollection AddStationsRuntime(this IServiceCollection services)
    {
        services.TryAddScoped<StationAdmissionCoordinator>();
        services.TryAddScoped<StationRuntimeService>();
        services.TryAddScoped<IStationSessionReader>(s => s.GetRequiredService<StationRuntimeService>());
        return services;
    }
}
