namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Persistence.Repositories;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Properties.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddDataRightsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);
        builder.Services.TryAddModuleDbContext<DataRightsDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                DataRightsMigrations.SqlServerAssembly,
                DataRightsMigrations.PostgreSqlAssembly,
                DataRightsMigrations.Schema,
                DataRightsMigrations.HistoryTable));
        builder.Services.TryAddScoped<IDataRightsCaseRepository, DataRightsCaseRepository>();
        builder.Services.TryAddScoped<
            IDataRightsExecutionWorkItemRepository,
            DataRightsExecutionWorkItemRepository>();
        builder.Services.TryAddScoped<
            IDataRightsPropertyProjectionRepository,
            DataRightsPropertyProjectionRepository>();
        builder.Services.TryAddScoped<
            IProjectionRebuildWriter<PropertyTopologyProjectionExport>,
            DataRightsPropertiesProjectionRebuildWriter>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(ICommandPipelineBehavior<,>),
            typeof(DataRightsPersistenceRetryBehavior<,>)));
        builder.Services.MoveCommandUnitOfWorkBehaviorToEnd();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IUnitOfWork, DataRightsUnitOfWork>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IOutboxWriter, DataRightsOutboxWriter>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IOutboxStore, DataRightsOutboxStore>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IInboxStore, DataRightsInboxStore>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IProjectionRebuildCheckpointStore,
                DataRightsProjectionRebuildCheckpointStore>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<
                IProjectionRebuildTransactionBoundary,
                DataRightsProjectionRebuildTransactionBoundary>());
        return builder;
    }
}
