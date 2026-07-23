namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Contracts;

public static class DataRightsMigrations
{
    public const string Schema = DataRightsModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "BunkFy.Modules.DataRights.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations";
}
