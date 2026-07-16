namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Properties.Contracts;
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
        builder.Services.TryAddScoped<AdapterIngressCredentialRepository>();
        builder.Services.TryAddScoped<IAdapterIngressCredentialRepository>(provider =>
            provider.GetRequiredService<AdapterIngressCredentialRepository>());
        builder.Services.TryAddScoped<IAdapterIngressCredentialReader>(provider =>
            provider.GetRequiredService<AdapterIngressCredentialRepository>());
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
        builder.Services.TryAddScoped<LegalHoldRepository>();
        builder.Services.TryAddScoped<ILegalHoldRepository>(provider =>
            provider.GetRequiredService<LegalHoldRepository>());
        builder.Services.TryAddScoped<ILegalHoldReader>(provider =>
            provider.GetRequiredService<LegalHoldRepository>());
        builder.Services.TryAddScoped<IReservationSourceLinkRepository, ReservationSourceLinkRepository>();
        builder.Services.TryAddScoped<IReservationDispatchRepository, ReservationDispatchRepository>();
        builder.Services.TryAddScoped<IChangeProposalRepository, ChangeProposalRepository>();
        builder.Services.TryAddScoped<IChangeProposalReader, ChangeProposalReader>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(ICommandPipelineBehavior<,>),
            typeof(IngestionPersistenceRetryBehavior<,>)));
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
}
