namespace BunkFy.Adapters.SmtpEmail;

using Gma.Framework.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IServiceCollection AddBunkFySmtpEmailSender(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(SmtpEmailOptions.SectionName);
        SmtpEmailOptions settings = section.Get<SmtpEmailOptions>() ?? new();
        SmtpEmailOptionsValidator validator = new(isProduction);
        ValidateOptionsResult validation = validator.Validate(null, settings);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                SmtpEmailOptions.SectionName,
                typeof(SmtpEmailOptions),
                validation.Failures);
        }

        services.AddOptions<SmtpEmailOptions>()
            .Bind(section)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SmtpEmailOptions>>(validator));

        if (settings.Enabled)
        {
            services.TryAddSingleton<ISmtpEmailTransport, MailKitSmtpEmailTransport>();
            services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        }

        return services;
    }
}
