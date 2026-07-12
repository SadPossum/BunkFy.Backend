namespace BunkFy.Modules.Reservations.Persistence;

using BunkFy.Modules.Reservations.Contracts;

public static class ReservationsMigrations
{
    public const string Schema = ReservationsModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "BunkFy.Modules.Reservations.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations";
}
