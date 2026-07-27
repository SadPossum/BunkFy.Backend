using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AllowAccessExportCompletionWithoutExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_execution",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_execution",
                schema: "data-rights",
                table: "cases",
                sql: "(\"ExecutionRevision\" IS NULL AND \"ExecutionStartedBy\" IS NULL AND \"ExecutionStartedAtUtc\" IS NULL AND (\"Status\" IN (1, 2, 3, 4, 5, 6, 11) OR (\"Status\" = 9 AND \"Decision\" = 1 AND \"RequestedOperations\" = 1))) OR (\"ExecutionRevision\" IS NOT NULL AND \"ExecutionRevision\" > \"DecisionRevision\" AND \"ExecutionRevision\" <= \"Version\" AND \"ExecutionStartedBy\" IS NOT NULL AND \"ExecutionStartedAtUtc\" IS NOT NULL AND \"Decision\" = 1 AND \"Status\" IN (7, 8, 9, 10, 11))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_execution",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_execution",
                schema: "data-rights",
                table: "cases",
                sql: "(\"ExecutionRevision\" IS NULL AND \"ExecutionStartedBy\" IS NULL AND \"ExecutionStartedAtUtc\" IS NULL AND \"Status\" IN (1, 2, 3, 4, 5, 6, 11)) OR (\"ExecutionRevision\" IS NOT NULL AND \"ExecutionRevision\" > \"DecisionRevision\" AND \"ExecutionRevision\" <= \"Version\" AND \"ExecutionStartedBy\" IS NOT NULL AND \"ExecutionStartedAtUtc\" IS NOT NULL AND \"Decision\" = 1 AND \"Status\" IN (7, 8, 9, 10, 11))");
        }
    }
}
