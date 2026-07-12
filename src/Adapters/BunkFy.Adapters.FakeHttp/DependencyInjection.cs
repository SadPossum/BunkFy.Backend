namespace BunkFy.Adapters.FakeHttp;

using BunkFy.Adapter.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IHttpClientBuilder AddFakeHttpAdapter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFakeHttpAdapterDescriptor();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAdapterRunner, FakeHttpAdapterRunner>());
        return services
            .AddHttpClient(FakeHttpAdapterRunner.AdapterType, client =>
                client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false
            });
    }

    public static IServiceCollection AddFakeHttpAdapterDescriptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAdapterDescriptorProvider, FakeHttpAdapterDescriptor>());
        return services;
    }
}
