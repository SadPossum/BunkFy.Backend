namespace Ordering.Persistence;

using Ordering.Contracts;

public static class OrderingMigrations
{
    public const string Schema = OrderingModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Ordering.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Ordering.Persistence.PostgreSqlMigrations";
}
