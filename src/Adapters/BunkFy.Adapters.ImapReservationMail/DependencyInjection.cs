namespace BunkFy.Adapters.ImapReservationMail;

using BunkFy.Adapter.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddImapReservationMailAdapter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddImapReservationMailAdapterDescriptor();
        services.TryAddSingleton<IImapMailboxClientFactory, MailKitImapMailboxClientFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IAdapterRunner, ImapReservationMailAdapterRunner>());
        return services;
    }

    public static IServiceCollection AddImapReservationMailAdapterDescriptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAdapterDescriptorProvider,
            ImapReservationMailAdapterDescriptor>());
        return services;
    }
}
