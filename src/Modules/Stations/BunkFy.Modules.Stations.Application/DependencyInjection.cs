namespace BunkFy.Modules.Stations.Application;

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
}

