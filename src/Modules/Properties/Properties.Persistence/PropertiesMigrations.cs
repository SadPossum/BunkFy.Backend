namespace Properties.Persistence;

using Properties.Contracts;

public static class PropertiesMigrations
{
    public const string Schema = PropertiesModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Properties.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Properties.Persistence.PostgreSqlMigrations";
}
