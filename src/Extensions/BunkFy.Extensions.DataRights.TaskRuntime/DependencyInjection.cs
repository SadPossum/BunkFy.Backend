namespace BunkFy.Extensions.DataRights.TaskRuntime;

using BunkFy.Modules.DataRights.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddBunkFyTaskRuntimeDataRights(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = TaskRuntimePersonalDataCatalog.Current;
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                ITenantTerminationContributor,
                TaskRuntimeTenantTerminationContributor>());
        return services;
    }
}
