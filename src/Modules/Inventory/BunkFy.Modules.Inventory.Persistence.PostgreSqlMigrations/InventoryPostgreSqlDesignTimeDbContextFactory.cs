namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations;

using BunkFy.Modules.Inventory.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class InventoryPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
        => new(
            DesignTimeDbContextOptionsFactory.CreatePostgreSqlOptions<InventoryDbContext>(
                args,
                InventoryMigrations.PostgreSqlAssembly,
                InventoryMigrations.Schema,
                InventoryMigrations.HistoryTable),
            new DesignTimeScopeContext());
}
