namespace Reservations.Persistence;

using Reservations.Contracts;

public static class ReservationsMigrations
{
    public const string Schema = ReservationsModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "Reservations.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "Reservations.Persistence.PostgreSqlMigrations";
}
