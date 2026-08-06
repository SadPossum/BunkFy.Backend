namespace BunkFy.Modules.Ingestion.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.ProjectionRebuild;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddIngestionPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);

        builder.Services.TryAddModuleDbContext<IngestionDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                IngestionMigrations.SqlServerAssembly,
                IngestionMigrations.PostgreSqlAssembly,
                IngestionMigrations.Schema,
                IngestionMigrations.HistoryTable));

        builder.Services.TryAddScoped<IAdapterConnectionRepository, AdapterConnectionRepository>();
        builder.Services.TryAddScoped<IIngestionExecutionLock, IngestionExecutionLock>();
        builder.Services.TryAddScoped<AdapterIngressCredentialRepository>();
        builder.Services.TryAddScoped<IAdapterIngressCredentialRepository>(provider =>
            provider.GetRequiredService<AdapterIngressCredentialRepository>());
        builder.Services.TryAddScoped<IAdapterIngressCredentialReader>(provider =>
            provider.GetRequiredService<AdapterIngressCredentialRepository>());
        builder.Services.TryAddScoped<IAdapterIngressControlRepository, AdapterIngressControlRepository>();
        builder.Services.TryAddScoped<IngestionPropertyProjectionRepository>();
        builder.Services.TryAddScoped<IIngestionPropertyProjectionRepository>(provider =>
            provider.GetRequiredService<IngestionPropertyProjectionRepository>());
        builder.Services.TryAddScoped<IRetentionFenceRepository>(provider =>
            provider.GetRequiredService<IngestionPropertyProjectionRepository>());
        builder.Services.TryAddScoped<IIngestionOperationsReader, IngestionOperationsReader>();
        builder.Services.TryAddScoped<IProjectionRebuildWriter<PropertyTopologyProjectionExport>,
            IngestionPropertyProjectionRebuildWriter>();
        builder.Services.TryAddScoped<IIngestionRunRepository, IngestionRunRepository>();
        builder.Services.TryAddScoped<IAdapterPollingScheduleReader, AdapterPollingScheduleReader>();
        builder.Services.TryAddScoped<IObservationReceiptRepository, ObservationReceiptRepository>();
        builder.Services.TryAddScoped<IObservationReprocessingAttemptRepository,
            ObservationReprocessingAttemptRepository>();
        builder.Services.TryAddScoped<IObservationReprocessingOutputRepository,
            ObservationReprocessingOutputRepository>();
        builder.Services.TryAddScoped<IRawPayloadRetentionRepository, RawPayloadRetentionRepository>();
        builder.Services.TryAddScoped<ISensitiveHistoryRetentionRepository, SensitiveHistoryRetentionRepository>();
        builder.Services.TryAddScoped<
            IIngestionRetentionExecutionRepository,
            IngestionRetentionExecutionRepository>();
        builder.Services.TryAddScoped<
            IIngestionRetentionStatusReader,
            IngestionRetentionStatusReader>();
        builder.Services.TryAddScoped<LegalHoldRepository>();
        builder.Services.TryAddScoped<ILegalHoldRepository>(provider =>
            provider.GetRequiredService<LegalHoldRepository>());
        builder.Services.TryAddScoped<ILegalHoldReader>(provider =>
            provider.GetRequiredService<LegalHoldRepository>());
        builder.Services.TryAddScoped<IReservationSourceLinkRepository, ReservationSourceLinkRepository>();
        builder.Services.TryAddScoped<
            IIngestionNotificationSourceLinkResolver,
            IngestionNotificationSourceLinkResolver>();
        builder.Services.TryAddScoped<IReservationDispatchRepository, ReservationDispatchRepository>();
        builder.Services.TryAddScoped<IChangeProposalRepository, ChangeProposalRepository>();
        builder.Services.TryAddScoped<IChangeProposalReader, ChangeProposalReader>();
        builder.Services.TryAddScoped<IngestionDataRightsEvidenceGraphLoader>();
        builder.Services.TryAddScoped<
            IIngestionAnonymisationEligibilityRepository,
            IngestionAnonymisationEligibilityRepository>();
        builder.Services.TryAddScoped<
            IIngestionAnonymisationBarrierRepository,
            IngestionAnonymisationBarrierRepository>();
        builder.Services.TryAddScoped<
            IIngestionAnonymisationRestoreRepository,
            IngestionAnonymisationRestoreRepository>();
        builder.Services.TryAddScoped<
            IIngestionSourceOperationLock,
            IngestionSourceOperationLockRepository>();
        AddAnonymisationFingerprintServices(builder);
        AddAnonymisationRestoreReadiness(builder);
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsSubjectDiscoveryContributor,
                IngestionDataRightsDiscoveryContributor>());
        IngestionDataRightsExportSchema.EnsureValid();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IDataRightsSubjectExportContributor,
                IngestionDataRightsExportContributor>());
        IngestionTenantTerminationExportSchema.EnsureValid();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                ITenantTerminationContributor,
                IngestionTenantTerminationContributor>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                ITenantTerminationExportContributor,
                IngestionTenantTerminationContributor>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(ICommandPipelineBehavior<,>),
            typeof(IngestionPersistenceRetryBehavior<,>)));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(ICommandPipelineBehavior<,>),
            typeof(IngestionPersistenceAdmissionBehavior<,>)));
        builder.Services.MoveCommandUnitOfWorkBehaviorToEnd();
        builder.Services.TryAddScoped<IRawPayloadStore, IngestionRawPayloadStore>();
        builder.Services.TryAddSingleton<IIngestionRetentionPolicy>(
            ConfiguredIngestionRetentionPolicy.FromConfiguration(builder.Configuration));

        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, IngestionUnitOfWork>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxWriter, IngestionOutboxWriter>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxStore, IngestionOutboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboxStore, IngestionInboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IProjectionRebuildCheckpointStore,
            IngestionProjectionRebuildCheckpointStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IProjectionRebuildTransactionBoundary,
            IngestionProjectionRebuildTransactionBoundary>());

        return builder;
    }

    private static void AddAnonymisationFingerprintServices(
        IHostApplicationBuilder builder)
    {
        bool isProduction = builder.Environment.IsProduction();
        IConfigurationSection section = builder.Configuration.GetSection(
            IngestionAnonymisationFingerprintOptions.SectionName);
        bool useDevelopmentKey = !isProduction && !section.Exists();
        builder.Services
            .AddOptions<IngestionAnonymisationFingerprintOptions>()
            .Bind(section)
            .PostConfigure(options =>
            {
                if (useDevelopmentKey)
                {
                    options.ActiveKeyVersion = 1;
                    options.Keys[1] =
                        IngestionAnonymisationFingerprintOptions
                            .DevelopmentKeyBase64;
                }
            })
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<
                    IngestionAnonymisationFingerprintOptions>>(
                new IngestionAnonymisationFingerprintOptionsValidator(
                    isProduction)));
        builder.Services.TryAddSingleton<
            IIngestionAnonymisationFingerprintService,
            HmacIngestionAnonymisationFingerprintService>();
    }

    private static void AddAnonymisationRestoreReadiness(
        IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<
            IngestionAnonymisationRestoreReadinessHealthCheck>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IConfigureOptions<HealthCheckServiceOptions>,
                IngestionAnonymisationRestoreHealthCheckOptionsSetup>());
    }
}
