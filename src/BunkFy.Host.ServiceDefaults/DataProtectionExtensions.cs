namespace BunkFy.Host.ServiceDefaults;

using BunkFy.Host.ServiceDefaults.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class DataProtectionExtensions
{
    public static IHostApplicationBuilder AddBunkFyDataProtection(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        BunkFyDataProtectionOptions configured = builder.Configuration
            .GetSection(BunkFyDataProtectionOptions.SectionName)
            .Get<BunkFyDataProtectionOptions>() ?? new();
        ValidateOptionsResult validation = new BunkFyDataProtectionOptionsValidator()
            .Validate(name: null, configured);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                BunkFyDataProtectionOptions.SectionName,
                typeof(BunkFyDataProtectionOptions),
                validation.Failures);
        }

        if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(configured.KeyRingPath))
        {
            throw new OptionsValidationException(
                BunkFyDataProtectionOptions.SectionName,
                typeof(BunkFyDataProtectionOptions),
                ["DataProtection:KeyRingPath is required in Production so protected authentication state survives restarts and works across replicas."]);
        }

        builder.Services
            .AddOptions<BunkFyDataProtectionOptions>()
            .Bind(builder.Configuration.GetSection(BunkFyDataProtectionOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IValidateOptions<BunkFyDataProtectionOptions>,
            BunkFyDataProtectionOptionsValidator>());

        IDataProtectionBuilder dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName(configured.ApplicationName.Trim());
        if (!string.IsNullOrWhiteSpace(configured.KeyRingPath))
        {
            string keyRingPath = Path.GetFullPath(
                configured.KeyRingPath.Trim(),
                builder.Environment.ContentRootPath);
            Directory.CreateDirectory(keyRingPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        }

        return builder;
    }
}
