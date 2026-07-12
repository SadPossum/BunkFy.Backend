namespace BunkFy.Modules.Properties.Persistence;

using BunkFy.Modules.Properties.Contracts;

public static class PropertiesMigrations
{
    public const string Schema = PropertiesModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "BunkFy.Modules.Properties.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "BunkFy.Modules.Properties.Persistence.PostgreSqlMigrations";
}
