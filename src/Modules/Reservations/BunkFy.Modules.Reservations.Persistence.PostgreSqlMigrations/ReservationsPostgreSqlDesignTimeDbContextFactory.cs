namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations;

using BunkFy.Modules.Reservations.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class ReservationsPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReservationsDbContext>
{
    public ReservationsDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<ReservationsDbContext>(
                args,
                ReservationsMigrations.PostgreSqlAssembly,
                ReservationsMigrations.Schema,
                ReservationsMigrations.HistoryTable),
            new DesignTimeScopeContext());
}
