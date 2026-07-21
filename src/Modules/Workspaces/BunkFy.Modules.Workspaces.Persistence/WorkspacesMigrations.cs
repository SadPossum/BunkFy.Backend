namespace BunkFy.Modules.Workspaces.Persistence;

using BunkFy.Modules.Workspaces.Contracts;

public static class WorkspacesMigrations
{
    public const string Schema = WorkspacesModuleMetadata.Schema;
    public const string HistoryTable = "__ef_migrations_history";
    public const string SqlServerAssembly = "BunkFy.Modules.Workspaces.Persistence.SqlServerMigrations";
    public const string PostgreSqlAssembly = "BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations";
}
