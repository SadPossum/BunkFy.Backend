namespace BunkFy.Modules.Retention.Persistence;

using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Persistence.Repositories;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddRetentionPersistence(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);
        builder.Services.TryAddModuleDbContext<RetentionDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                RetentionMigrations.SqlServerAssembly,
                RetentionMigrations.PostgreSqlAssembly,
                RetentionMigrations.Schema,
                RetentionMigrations.HistoryTable));
        builder.Services.TryAddScoped<
            IRetentionExecutionRepository,
            RetentionExecutionRepository>();
        builder.Services.TryAddScoped<
            IRetentionScopeRepository,
            RetentionScopeRepository>();
        builder.Services.TryAddScoped<RetentionScheduleStateRepository>();
        builder.Services.TryAddScoped<IRetentionScheduleStateRepository>(provider =>
            provider.GetRequiredService<RetentionScheduleStateRepository>());
        builder.Services.TryAddScoped<IRetentionScheduleHealthReader>(provider =>
            provider.GetRequiredService<RetentionScheduleStateRepository>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IUnitOfWork, RetentionUnitOfWork>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IInboxStore, RetentionInboxStore>());
        return builder;
    }
}
