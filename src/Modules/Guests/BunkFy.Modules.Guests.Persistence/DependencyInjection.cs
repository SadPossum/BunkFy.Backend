namespace BunkFy.Modules.Guests.Persistence;

using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using BunkFy.Modules.Properties.Contracts;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddGuestsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);
        builder.Services.TryAddModuleDbContext<GuestsDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                GuestsMigrations.SqlServerAssembly,
                GuestsMigrations.PostgreSqlAssembly,
                GuestsMigrations.Schema,
                GuestsMigrations.HistoryTable));
        builder.Services.TryAddScoped<IGuestProfileRepository, GuestProfileRepository>();
        builder.Services.TryAddScoped<IGuestProfileEligibilityProjectionExportSource, GuestProfileEligibilityProjectionExportSource>();
        builder.Services.TryAddScoped<IGuestPropertyProjectionRepository, GuestPropertyProjectionRepository>();
        builder.Services.TryAddScoped<IGuestStayHistoryRepository, GuestStayHistoryRepository>();
        builder.Services.TryAddScoped<IProjectionRebuildWriter<PropertyTopologyProjectionExport>, GuestsPropertiesProjectionRebuildWriter>();
        builder.Services.TryAddScoped<IProjectionRebuildWriter<ReservationGuestStayProjectionExport>, GuestStayHistoryProjectionRebuildWriter>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, GuestsUnitOfWork>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxWriter, GuestsOutboxWriter>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxStore, GuestsOutboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboxStore, GuestsInboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IProjectionRebuildCheckpointStore, GuestsProjectionRebuildCheckpointStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IProjectionRebuildTransactionBoundary, GuestsProjectionRebuildTransactionBoundary>());
        return builder;
    }
}
