namespace BunkFy.Modules.Retention.Persistence;

using BunkFy.Modules.Retention.Contracts;

public static class RetentionMigrations
{
    public const string Schema = RetentionModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "BunkFy.Modules.Retention.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "BunkFy.Modules.Retention.Persistence.PostgreSqlMigrations";
}
