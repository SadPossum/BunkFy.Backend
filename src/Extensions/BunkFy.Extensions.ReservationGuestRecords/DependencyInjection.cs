namespace BunkFy.Extensions.ReservationGuestRecords;

using Gma.Framework.AccessControl.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddBunkFyReservationGuestRecords(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ReservationGuestRecordWorkflow>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IAccessHttpScopeResolver,
                ReservationGuestRecordPropertyAccessScopeResolver>());
        return services;
    }
}
