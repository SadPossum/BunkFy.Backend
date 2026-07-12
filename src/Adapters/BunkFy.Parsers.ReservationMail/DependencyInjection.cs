namespace BunkFy.Parsers.ReservationMail;

using BunkFy.ObservationParsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddReservationMailParser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddReservationMailParserDescriptor();
        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IObservationParser, ReservationMailObservationParser>());
        return services;
    }

    public static IServiceCollection AddReservationMailParserDescriptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IObservationParserDescriptorProvider,
            ReservationMailParserDescriptor>());
        return services;
    }
}
