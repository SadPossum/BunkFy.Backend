namespace Inventory.Persistence;

using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.ProjectionRebuild;
using Inventory.Application.Ports;
using Inventory.Contracts;
using Inventory.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Properties.Contracts;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInventoryPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);
        builder.Services.TryAddModuleDbContext<InventoryDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                InventoryMigrations.SqlServerAssembly,
                InventoryMigrations.PostgreSqlAssembly,
                InventoryMigrations.Schema,
                InventoryMigrations.HistoryTable));

        builder.Services.TryAddScoped<IInventoryTopologyRepository, InventoryTopologyRepository>();
        builder.Services.TryAddScoped<IRoomInventoryConfigurationRepository, RoomInventoryConfigurationRepository>();
        builder.Services.TryAddScoped<IInventoryReadRepository, InventoryReadRepository>();
        builder.Services.TryAddScoped<IManualInventoryBlockRepository, ManualInventoryBlockRepository>();
        builder.Services.TryAddScoped<IInventoryAvailabilityProjectionExportSource, InventoryAvailabilityProjectionExportSource>();
        builder.Services.TryAddScoped<IInventoryAllocationRepository, InventoryAllocationRepository>();
        builder.Services.TryAddScoped<IProjectionRebuildWriter<PropertyTopologyProjectionExport>, InventoryTopologyProjectionRebuildWriter>();
        builder.Services.TryAddEnumerable([
            ServiceDescriptor.Scoped<IUnitOfWork, InventoryUnitOfWork>(),
            ServiceDescriptor.Scoped<IInboxStore, InventoryInboxStore>(),
            ServiceDescriptor.Scoped<IOutboxWriter, InventoryOutboxWriter>(),
            ServiceDescriptor.Scoped<IOutboxStore, InventoryOutboxStore>(),
            ServiceDescriptor.Scoped<IProjectionRebuildCheckpointStore, InventoryProjectionRebuildCheckpointStore>(),
            ServiceDescriptor.Scoped<IProjectionRebuildTransactionBoundary, InventoryProjectionRebuildTransactionBoundary>()
        ]);

        return builder;
    }
}
