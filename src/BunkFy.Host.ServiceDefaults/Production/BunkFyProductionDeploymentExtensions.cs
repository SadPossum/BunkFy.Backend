namespace BunkFy.Host.ServiceDefaults.Production;

using Gma.Framework.Api.Production;
using Gma.Framework.FileManagement.Minio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class BunkFyProductionDeploymentExtensions
{
    public static IHostApplicationBuilder AddBunkFyProductionDeployment(
        this IHostApplicationBuilder builder,
        BunkFyDeploymentSurface surface)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(BunkFyDeploymentRegistrationMarker)))
        {
            return builder;
        }

        IConfigurationSection section = builder.Configuration
            .GetSection(BunkFyDeploymentOptions.SectionName);
        BunkFyDeploymentOptions options =
            section.Get<BunkFyDeploymentOptions>() ?? new();
        string[] failures = BunkFyDeploymentOptionsValidation.Validate(
            options,
            builder.Environment.IsProduction(),
            surface,
            builder.Configuration.GetSection(ProductionHttpOptions.SectionName)
                .Get<ProductionHttpOptions>() ?? new(),
            builder.Configuration.GetSection(ProductionDataProtectionOptions.SectionName)
                .Get<ProductionDataProtectionOptions>() ?? new(),
            builder.Configuration.GetSection(MinioFileStorageOptions.SectionName)
                .Get<MinioFileStorageOptions>() ?? new(),
            builder.Configuration.GetValue<bool>("FileManagement:Enabled"));
        if (failures.Length > 0)
        {
            throw new OptionsValidationException(
                BunkFyDeploymentOptions.SectionName,
                typeof(BunkFyDeploymentOptions),
                failures);
        }

        builder.Services.AddSingleton<BunkFyDeploymentRegistrationMarker>();
        builder.Services.AddSingleton(new BunkFyDeploymentSurfaceRegistration(surface));
        builder.Services
            .AddOptions<BunkFyDeploymentOptions>()
            .Bind(section)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<BunkFyDeploymentOptions>,
                BunkFyDeploymentOptionsValidator>());

        return builder;
    }

    private sealed class BunkFyDeploymentRegistrationMarker;
}
