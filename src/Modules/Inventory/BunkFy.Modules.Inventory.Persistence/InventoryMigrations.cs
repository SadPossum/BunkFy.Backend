namespace BunkFy.Modules.Inventory.Persistence;

using BunkFy.Modules.Inventory.Contracts;

public static class InventoryMigrations
{
    public const string Schema = InventoryModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "BunkFy.Modules.Inventory.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations";
}
