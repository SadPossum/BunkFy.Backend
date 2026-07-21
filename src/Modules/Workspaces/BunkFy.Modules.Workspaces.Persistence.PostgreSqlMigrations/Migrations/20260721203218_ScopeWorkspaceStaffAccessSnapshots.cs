using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ScopeWorkspaceStaffAccessSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_staff_access_profile_snapshots",
                schema: "workspaces",
                table: "staff_access_profile_snapshots");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentScope",
                schema: "workspaces",
                table: "staff_access_profile_snapshots",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE workspaces.staff_access_profile_snapshots AS snapshot
                SET "AssignmentScope" = 'tenant:' || process."ScopeId"
                FROM workspaces.staff_access_processes AS process
                WHERE process."Id" = snapshot."ProcessId";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "AssignmentScope",
                schema: "workspaces",
                table: "staff_access_profile_snapshots",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_staff_access_profile_snapshots",
                schema: "workspaces",
                table: "staff_access_profile_snapshots",
                columns: new[] { "ProcessId", "ProfileId", "AssignmentScope" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_staff_access_profile_snapshots",
                schema: "workspaces",
                table: "staff_access_profile_snapshots");

            migrationBuilder.Sql(
                """
                DELETE FROM workspaces.staff_access_profile_snapshots AS duplicate
                USING workspaces.staff_access_profile_snapshots AS retained
                WHERE duplicate."ProcessId" = retained."ProcessId"
                  AND duplicate."ProfileId" = retained."ProfileId"
                  AND (
                      duplicate."AssignmentScope" > retained."AssignmentScope"
                      OR (
                          duplicate."AssignmentScope" = retained."AssignmentScope"
                          AND duplicate.ctid > retained.ctid));
                """);

            migrationBuilder.DropColumn(
                name: "AssignmentScope",
                schema: "workspaces",
                table: "staff_access_profile_snapshots");

            migrationBuilder.AddPrimaryKey(
                name: "PK_staff_access_profile_snapshots",
                schema: "workspaces",
                table: "staff_access_profile_snapshots",
                columns: new[] { "ProcessId", "ProfileId" });
        }
    }
}
