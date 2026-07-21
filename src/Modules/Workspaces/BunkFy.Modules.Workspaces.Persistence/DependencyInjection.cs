namespace BunkFy.Modules.Workspaces.Persistence;

using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Persistence.Repositories;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddWorkspacesPersistence(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddPersistenceOptions(builder.Configuration);
        builder.Services.TryAddModuleDbContext<WorkspacesDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                WorkspacesMigrations.SqlServerAssembly,
                WorkspacesMigrations.PostgreSqlAssembly,
                WorkspacesMigrations.Schema,
                WorkspacesMigrations.HistoryTable));
        builder.Services.TryAddScoped<
            IWorkspaceStaffOnboardingRepository,
            WorkspaceStaffOnboardingRepository>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IUnitOfWork, WorkspacesUnitOfWork>());
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IInboxStore, WorkspacesInboxStore>());
        return builder;
    }
}
