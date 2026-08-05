namespace BunkFy.Extensions.DataRights.TenantTermination;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Application.Production;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IHostApplicationBuilder
        AddBunkFyTenantTerminationProductionAdmission(
            this IHostApplicationBuilder builder,
            bool dataRightsComposed,
            bool completeOwnerTopology,
            bool taskWorkerEnabled)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(descriptor => descriptor.ServiceType ==
                typeof(TenantTerminationProductionAdmissionMarker)))
        {
            throw new InvalidOperationException(
                "Tenant-termination production admission is already registered.");
        }

        IConfigurationSection section = builder.Configuration.GetSection(
            TenantTerminationProductionAdmissionOptions.SectionName);
        TenantTerminationProductionAdmissionOptions options = section.Get<
            TenantTerminationProductionAdmissionOptions>() ?? new();
        IReadOnlySet<string> workerGroups = builder.Configuration
            .GetSection("Tasks:Worker:WorkerGroups")
            .Get<string[]>()?
            .Select(group => group?.Trim() ?? string.Empty)
            .Where(group => group.Length > 0)
            .ToHashSet(StringComparer.Ordinal) ??
            new HashSet<string>(StringComparer.Ordinal);
        TenantTerminationProductionAdmissionRegistration registration = new(
            builder.Environment.IsProduction(),
            dataRightsComposed,
            completeOwnerTopology,
            taskWorkerEnabled,
            workerGroups);
        TenantTerminationProductionAdmissionValidator validator = new(
            registration);
        ValidateOptionsResult validation = validator.Validate(null, options);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                TenantTerminationProductionAdmissionOptions.SectionName,
                typeof(TenantTerminationProductionAdmissionOptions),
                validation.Failures);
        }

        builder.Services.AddSingleton<
            TenantTerminationProductionAdmissionMarker>();
        builder.Services.AddBunkFyTenantTerminationOperatorCatalog();
        builder.Services.AddSingleton(registration);
        builder.Services
            .AddOptions<TenantTerminationProductionAdmissionOptions>()
            .Bind(section)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<
                    TenantTerminationProductionAdmissionOptions>>(
                validator));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IHostedService,
                TenantTerminationProductionAdmissionStartupValidator>());
        return builder;
    }

    public static IServiceCollection
        AddBunkFyTenantTerminationOperatorCatalog(
            this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<
            ITenantTerminationRequiredOwnerCatalog,
            BunkFyTenantTerminationRequiredOwnerCatalog>();
        return services;
    }

    private sealed class TenantTerminationProductionAdmissionMarker;
}
