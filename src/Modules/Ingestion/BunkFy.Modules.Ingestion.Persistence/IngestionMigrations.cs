namespace BunkFy.Modules.Ingestion.Persistence;

using BunkFy.Modules.Ingestion.Contracts;

public static class IngestionMigrations
{
    public const string Schema = IngestionModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "BunkFy.Modules.Ingestion.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations";
}
