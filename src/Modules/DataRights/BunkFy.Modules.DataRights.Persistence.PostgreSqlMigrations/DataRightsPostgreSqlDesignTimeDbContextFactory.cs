namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations;

using BunkFy.Modules.DataRights.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class DataRightsPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<DataRightsDbContext>
{
    public DataRightsDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<DataRightsDbContext>(
                args,
                DataRightsMigrations.PostgreSqlAssembly,
                DataRightsMigrations.Schema,
                DataRightsMigrations.HistoryTable),
            new DesignTimeScopeContext());
}
