namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class BunkFyDurableRuntimeProductionAdmissionExtensions
{
    public static IHostApplicationBuilder AddBunkFyDurableRuntimeProductionAdmission(
        this IHostApplicationBuilder builder,
        BunkFyDurableRuntimeHostRole hostRole,
        bool taskRuntimeComposed)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!Enum.IsDefined(hostRole))
        {
            throw new ArgumentOutOfRangeException(nameof(hostRole));
        }

        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType ==
                typeof(BunkFyDurableRuntimeProductionAdmissionMarker)))
        {
            throw new InvalidOperationException(
                "Durable runtime production admission is already registered.");
        }

        BunkFyDurableRuntimeProductionAdmissionRegistration registration = new(
            builder.Environment.IsProduction(),
            hostRole,
            taskRuntimeComposed,
            builder.Configuration.GetValue<bool>("Tasks:Worker:Enabled"),
            builder.Configuration.GetValue<bool>("Tasks:Scheduler:Enabled"),
            builder.Configuration.GetValue<bool>("NatsJetStream:Enabled"),
            builder.Configuration.GetValue<bool>("NatsConsumers:Enabled"));
        BunkFyDurableRuntimeRuntimeOptions runtime = ReadRuntime(builder.Configuration);
        IConfigurationSection section = builder.Configuration.GetSection(
            BunkFyDurableRuntimeProductionAdmissionOptions.SectionName);
        BunkFyDurableRuntimeProductionAdmissionOptions options =
            section.Get<BunkFyDurableRuntimeProductionAdmissionOptions>() ?? new();
        BunkFyDurableRuntimeProductionAdmissionValidator validator =
            new(registration, runtime);
        ValidateOptionsResult validation = validator.Validate(name: null, options);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                BunkFyDurableRuntimeProductionAdmissionOptions.SectionName,
                typeof(BunkFyDurableRuntimeProductionAdmissionOptions),
                validation.Failures);
        }

        builder.Services.AddSingleton<
            BunkFyDurableRuntimeProductionAdmissionMarker>();
        builder.Services.AddSingleton(registration);
        builder.Services.AddSingleton(runtime);
        builder.Services
            .AddOptions<BunkFyDurableRuntimeProductionAdmissionOptions>()
            .Bind(section)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<
                    BunkFyDurableRuntimeProductionAdmissionOptions>>(
                validator));
        if (registration.IsProduction)
        {
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IHostedService,
                    BunkFyDurableRuntimeProductionAdmissionReporter>());
        }

        return builder;
    }

    private static BunkFyDurableRuntimeRuntimeOptions ReadRuntime(
        IConfiguration configuration) =>
        new(
            configuration.GetValue<bool>("MessageJournalCleanup:Enabled"),
            configuration.GetValue("MessageJournalCleanup:CleanupProcessedOutbox", true),
            configuration.GetValue("MessageJournalCleanup:CleanupProcessedInbox", true),
            configuration.GetValue("MessageJournalCleanup:ProcessedOutboxRetention", TimeSpan.FromDays(7)),
            configuration.GetValue("MessageJournalCleanup:ProcessedInboxRetention", TimeSpan.FromDays(14)),
            configuration.GetValue("MessageJournalCleanup:BrokerReplayHorizon", TimeSpan.FromDays(7)),
            configuration.GetValue<bool>("TaskRuntimeRetention:Enabled"),
            configuration.GetValue("TaskRuntimeRetention:SucceededRunRetention", TimeSpan.FromDays(30)),
            configuration.GetValue("TaskRuntimeRetention:FailedRunRetention", TimeSpan.FromDays(90)),
            configuration.GetValue("TaskRuntimeRetention:CanceledRunRetention", TimeSpan.FromDays(30)),
            configuration.GetValue("TaskRuntimeRetention:TimedOutRunRetention", TimeSpan.FromDays(90)),
            configuration.GetValue("TaskRuntimeRetention:HandledControlRetention", TimeSpan.FromDays(30)),
            configuration.GetValue("TaskRuntimeRetention:FailedControlRetention", TimeSpan.FromDays(90)),
            configuration.GetValue("TaskRuntimeRetention:ExpiredControlRetention", TimeSpan.FromDays(30)));

    private sealed class BunkFyDurableRuntimeProductionAdmissionMarker;
}
