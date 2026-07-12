namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations;

using BunkFy.Modules.Staff.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class StaffPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<StaffDbContext>
{
    public StaffDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<StaffDbContext>(
                args,
                StaffMigrations.PostgreSqlAssembly,
                StaffMigrations.Schema,
                StaffMigrations.HistoryTable),
            new DesignTimeScopeContext());
}
