namespace BunkFy.Modules.Guests.Persistence;

using BunkFy.Modules.Guests.Contracts;

public static class GuestsMigrations
{
    public const string Schema = GuestsModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "BunkFy.Modules.Guests.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations";
}
