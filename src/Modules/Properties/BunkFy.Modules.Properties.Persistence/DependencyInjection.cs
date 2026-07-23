namespace BunkFy.Modules.Properties.Persistence;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Persistence.Repositories;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddPropertiesPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);

        builder.Services.TryAddModuleDbContext<PropertiesDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                PropertiesMigrations.SqlServerAssembly,
                PropertiesMigrations.PostgreSqlAssembly,
                PropertiesMigrations.Schema,
                PropertiesMigrations.HistoryTable));

        builder.Services.TryAddScoped<IPropertyRepository, PropertyRepository>();
        builder.Services.TryAddScoped<IRoomRepository, RoomRepository>();
        builder.Services.TryAddScoped<IPropertiesReadRepository, PropertiesReadRepository>();
        builder.Services.TryAddScoped<IPropertyGovernanceRevisionWriter, PropertyGovernanceRevisionWriter>();
        builder.Services.TryAddScoped<IPropertiesTopologyProjectionExportSource, PropertiesTopologyProjectionExportSource>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, PropertiesUnitOfWork>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboxStore, PropertiesInboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxWriter, PropertiesOutboxWriter>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxStore, PropertiesOutboxStore>());

        return builder;
    }
}
