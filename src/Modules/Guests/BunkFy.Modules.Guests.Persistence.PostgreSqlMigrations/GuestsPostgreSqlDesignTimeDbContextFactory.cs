namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations;

using BunkFy.Modules.Guests.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class GuestsPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<GuestsDbContext>
{
    public GuestsDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<GuestsDbContext>(
                args,
                GuestsMigrations.PostgreSqlAssembly,
                GuestsMigrations.Schema,
                GuestsMigrations.HistoryTable),
            new DesignTimeScopeContext());
}
