namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class BunkFyAuthRetentionProductionAdmissionExtensions
{
    public static IHostApplicationBuilder AddBunkFyAuthRetentionProductionAdmission(
        this IHostApplicationBuilder builder,
        BunkFyDeploymentSurface hostSurface,
        bool authComposed)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!Enum.IsDefined(hostSurface))
        {
            throw new ArgumentOutOfRangeException(nameof(hostSurface));
        }

        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType ==
                typeof(BunkFyAuthRetentionProductionAdmissionMarker)))
        {
            throw new InvalidOperationException(
                "Auth retention production admission is already registered.");
        }

        BunkFyAuthRetentionProductionAdmissionRegistration registration = new(
            builder.Environment.IsProduction(),
            hostSurface,
            authComposed);
        BunkFyAuthRetentionRuntimeOptions runtime = ReadRuntime(builder.Configuration);
        IConfigurationSection section = builder.Configuration.GetSection(
            BunkFyAuthRetentionProductionAdmissionOptions.SectionName);
        BunkFyAuthRetentionProductionAdmissionOptions options =
            section.Get<BunkFyAuthRetentionProductionAdmissionOptions>() ?? new();
        BunkFyAuthRetentionProductionAdmissionValidator validator =
            new(registration, runtime);
        ValidateOptionsResult validation = validator.Validate(name: null, options);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                BunkFyAuthRetentionProductionAdmissionOptions.SectionName,
                typeof(BunkFyAuthRetentionProductionAdmissionOptions),
                validation.Failures);
        }

        builder.Services.AddSingleton<BunkFyAuthRetentionProductionAdmissionMarker>();
        builder.Services.AddSingleton(registration);
        builder.Services.AddSingleton(runtime);
        builder.Services
            .AddOptions<BunkFyAuthRetentionProductionAdmissionOptions>()
            .Bind(section)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<BunkFyAuthRetentionProductionAdmissionOptions>>(
                validator));
        if (registration.IsProduction)
        {
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IHostedService,
                    BunkFyAuthRetentionProductionAdmissionReporter>());
        }

        return builder;
    }

    private static BunkFyAuthRetentionRuntimeOptions ReadRuntime(
        IConfiguration configuration) =>
        new(
            configuration.GetValue<bool>("Auth:Retention:Enabled"),
            configuration.GetValue("Auth:Retention:ExpiredExchangeHistoryHours", 24),
            configuration.GetValue("Auth:Retention:PasswordRecoveryHistoryHours", 24),
            configuration.GetValue("Auth:Retention:SessionHistoryDays", 365),
            configuration.GetValue("Auth:Retention:AuthenticationChallengeHistoryHours", 24),
            configuration.GetValue("Auth:Retention:ExpiredTotpEnrollmentHistoryHours", 24),
            configuration.GetValue("Auth:Retention:DisabledTotpAuthenticatorHistoryDays", 365),
            configuration.GetValue("Auth:Retention:MultiFactorFailureHistoryHours", 24),
            configuration.GetValue("Auth:Retention:AuthenticationFailureHistoryHours", 24));

    private sealed class BunkFyAuthRetentionProductionAdmissionMarker;
}
