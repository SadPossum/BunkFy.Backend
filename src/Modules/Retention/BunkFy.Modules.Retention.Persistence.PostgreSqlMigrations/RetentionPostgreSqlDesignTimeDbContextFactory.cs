namespace BunkFy.Modules.Retention.Persistence.PostgreSqlMigrations;

using BunkFy.Modules.Retention.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class RetentionPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RetentionDbContext>
{
    public RetentionDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<RetentionDbContext>(
                args,
                RetentionMigrations.PostgreSqlAssembly,
                RetentionMigrations.Schema,
                RetentionMigrations.HistoryTable),
            new DesignTimeScopeContext());
}
