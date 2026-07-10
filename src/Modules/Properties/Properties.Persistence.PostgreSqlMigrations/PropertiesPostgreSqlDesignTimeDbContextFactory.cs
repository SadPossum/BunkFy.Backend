namespace Properties.Persistence.PostgreSqlMigrations;

using Properties.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class PropertiesPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PropertiesDbContext>
{
    public PropertiesDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<PropertiesDbContext>(
                args,
                PropertiesMigrations.PostgreSqlAssembly,
                PropertiesMigrations.Schema,
                PropertiesMigrations.HistoryTable),
            new DesignTimeScopeContext());
}
