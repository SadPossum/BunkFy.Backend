namespace BunkFy.Modules.Staff.Persistence;

using BunkFy.Modules.Staff.Contracts;

public static class StaffMigrations
{
    public const string Schema = StaffModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "BunkFy.Modules.Staff.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations";
}
