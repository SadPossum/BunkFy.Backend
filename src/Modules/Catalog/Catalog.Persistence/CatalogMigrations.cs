namespace Catalog.Persistence;

using Catalog.Contracts;

public static class CatalogMigrations
{
    public const string Schema = CatalogModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Catalog.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Catalog.Persistence.PostgreSqlMigrations";
}
