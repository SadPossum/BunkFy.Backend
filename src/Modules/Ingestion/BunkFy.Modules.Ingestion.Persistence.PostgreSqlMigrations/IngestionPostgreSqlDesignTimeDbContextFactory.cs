namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations;

using BunkFy.Modules.Ingestion.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class IngestionPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IngestionDbContext>
{
    public IngestionDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<IngestionDbContext>(
                args,
                IngestionMigrations.PostgreSqlAssembly,
                IngestionMigrations.Schema,
                IngestionMigrations.HistoryTable),
            new DesignTimeScopeContext());
}
