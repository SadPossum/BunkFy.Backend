namespace Reservations.Persistence;

using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reservations.Application.Ports;
using Reservations.Persistence.Repositories;
using Gma.Framework.ProjectionRebuild;
using Inventory.Contracts;
public static class DependencyInjection
{
    public static IHostApplicationBuilder AddReservationsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);

        builder.Services.TryAddModuleDbContext<ReservationsDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                ReservationsMigrations.SqlServerAssembly,
                ReservationsMigrations.PostgreSqlAssembly,
                ReservationsMigrations.Schema,
                ReservationsMigrations.HistoryTable));

        builder.Services.TryAddScoped<IReservationRepository, ReservationRepository>();
        builder.Services.TryAddScoped<IInventoryProjectionRepository, InventoryProjectionRepository>();
        builder.Services.TryAddScoped<IProjectionRebuildWriter<InventoryAvailabilityProjectionExport>, InventoryProjectionRebuildWriter>();

        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, ReservationsUnitOfWork>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxWriter, ReservationsOutboxWriter>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxStore, ReservationsOutboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboxStore, ReservationsInboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IProjectionRebuildCheckpointStore, ReservationsProjectionRebuildCheckpointStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IProjectionRebuildTransactionBoundary, ReservationsProjectionRebuildTransactionBoundary>());

        return builder;
    }
}
