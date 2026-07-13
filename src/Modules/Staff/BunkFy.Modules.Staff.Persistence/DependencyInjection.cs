namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.ProjectionRebuild;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Persistence.Repositories;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddStaffPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddPersistenceOptions(builder.Configuration);
        builder.Services.TryAddModuleDbContext<StaffDbContext>(options => options.UseConfiguredProvider(
            builder.Configuration, StaffMigrations.SqlServerAssembly, StaffMigrations.PostgreSqlAssembly,
            StaffMigrations.Schema, StaffMigrations.HistoryTable));
        builder.Services.TryAddScoped<IStaffMemberRepository, StaffMemberRepository>();
        builder.Services.TryAddScoped<IStaffPropertyAudienceReader, StaffPropertyAudienceReader>();
        builder.Services.TryAddScoped<IStaffPropertyProjectionRepository, StaffPropertyProjectionRepository>();
        builder.Services.TryAddScoped<IProjectionRebuildWriter<PropertyTopologyProjectionExport>,
            StaffPropertiesProjectionRebuildWriter>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, StaffUnitOfWork>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxWriter, StaffOutboxWriter>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxStore, StaffOutboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboxStore, StaffInboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IProjectionRebuildCheckpointStore,
            StaffProjectionRebuildCheckpointStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IProjectionRebuildTransactionBoundary,
            StaffProjectionRebuildTransactionBoundary>());
        return builder;
    }
}
