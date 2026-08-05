namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class BunkFyOrganizationsMaintenanceProductionAdmissionExtensions
{
    public static IHostApplicationBuilder AddBunkFyOrganizationsMaintenanceProductionAdmission(
        this IHostApplicationBuilder builder,
        BunkFyDeploymentSurface hostSurface,
        bool organizationsComposed)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!Enum.IsDefined(hostSurface))
        {
            throw new ArgumentOutOfRangeException(nameof(hostSurface));
        }

        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType ==
                typeof(BunkFyOrganizationsMaintenanceProductionAdmissionMarker)))
        {
            throw new InvalidOperationException(
                "Organizations maintenance production admission is already registered.");
        }

        BunkFyOrganizationsMaintenanceProductionAdmissionRegistration registration = new(
            builder.Environment.IsProduction(),
            hostSurface,
            organizationsComposed);
        BunkFyOrganizationsMaintenanceRuntimeOptions runtime =
            ReadRuntime(builder.Configuration);
        IConfigurationSection section = builder.Configuration.GetSection(
            BunkFyOrganizationsMaintenanceProductionAdmissionOptions.SectionName);
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions options =
            section.Get<BunkFyOrganizationsMaintenanceProductionAdmissionOptions>() ?? new();
        BunkFyOrganizationsMaintenanceProductionAdmissionValidator validator =
            new(registration, runtime);
        ValidateOptionsResult validation = validator.Validate(name: null, options);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                BunkFyOrganizationsMaintenanceProductionAdmissionOptions.SectionName,
                typeof(BunkFyOrganizationsMaintenanceProductionAdmissionOptions),
                validation.Failures);
        }

        builder.Services.AddSingleton<
            BunkFyOrganizationsMaintenanceProductionAdmissionMarker>();
        builder.Services.AddSingleton(registration);
        builder.Services.AddSingleton(runtime);
        builder.Services
            .AddOptions<BunkFyOrganizationsMaintenanceProductionAdmissionOptions>()
            .Bind(section)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<
                    BunkFyOrganizationsMaintenanceProductionAdmissionOptions>>(
                validator));
        if (registration.IsProduction)
        {
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IHostedService,
                    BunkFyOrganizationsMaintenanceProductionAdmissionReporter>());
        }

        return builder;
    }

    private static BunkFyOrganizationsMaintenanceRuntimeOptions ReadRuntime(
        IConfiguration configuration) =>
        new(
            configuration.GetValue<bool>("Organizations:Lifecycle:Enabled"),
            configuration.GetValue<bool>("Organizations:Retention:Enabled"),
            configuration.GetValue("Organizations:Retention:InvitationHistoryDays", 90),
            configuration.GetValue("Organizations:Retention:EnrollmentHistoryDays", 90));

    private sealed class BunkFyOrganizationsMaintenanceProductionAdmissionMarker;
}
