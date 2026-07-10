namespace Inventory.Persistence;

using Inventory.Contracts;

public static class InventoryMigrations
{
    public const string Schema = InventoryModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Inventory.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Inventory.Persistence.PostgreSqlMigrations";
}
