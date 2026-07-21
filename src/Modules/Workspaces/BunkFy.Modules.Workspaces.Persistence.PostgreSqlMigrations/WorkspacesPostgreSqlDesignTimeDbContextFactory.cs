namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations;

using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class WorkspacesPostgreSqlDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<WorkspacesDbContext>
{
    public WorkspacesDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<WorkspacesDbContext> options = new();
        options.UseNpgsql(
            "Host=localhost;Port=5432;Database=bunkfy_design;Username=postgres;Password=postgres",
            provider => provider.MigrationsAssembly(WorkspacesMigrations.PostgreSqlAssembly)
                .MigrationsHistoryTable(
                    WorkspacesMigrations.HistoryTable,
                    WorkspacesMigrations.Schema));
        return new WorkspacesDbContext(options.Options, new DesignTimeScopeContext());
    }
}
