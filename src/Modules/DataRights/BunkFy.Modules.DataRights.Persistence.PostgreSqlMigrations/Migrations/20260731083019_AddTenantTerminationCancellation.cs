using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTerminationCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_completion",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_operation",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_outcome",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_phase",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_status",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_completion",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "(\"Phase\" = 5 AND \"Status\" = 5) OR (\"Phase\" = 6 AND \"Status\" IN (1, 2, 3, 4, 6)) OR (\"Phase\" BETWEEN 1 AND 4 AND \"Status\" BETWEEN 1 AND 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_operation",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"OperationRevision\" >= 0 AND ((\"Status\" = 1 AND \"OperationRevision\" >= 0) OR (\"Status\" BETWEEN 2 AND 6 AND \"OperationRevision\" >= 1))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_outcome",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "(\"Status\" = 3 AND \"OutcomeCode\" IS NOT NULL AND \"HoldReviewAtUtc\" IS NOT NULL AND \"HoldReviewAtUtc\" >= \"LastChangedAtUtc\") OR (\"Status\" = 4 AND \"OutcomeCode\" IS NOT NULL AND \"HoldReviewAtUtc\" IS NULL) OR (\"Status\" IN (1, 2, 5, 6) AND \"OutcomeCode\" IS NULL AND \"HoldReviewAtUtc\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_phase",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"Phase\" BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_status",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"Status\" BETWEEN 1 AND 6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_completion",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_operation",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_outcome",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_phase",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_status",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_completion",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "(\"Phase\" = 5 AND \"Status\" = 5) OR (\"Phase\" BETWEEN 1 AND 4 AND \"Status\" BETWEEN 1 AND 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_operation",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"OperationRevision\" >= 0 AND ((\"Status\" = 1 AND \"OperationRevision\" >= 0) OR (\"Status\" BETWEEN 2 AND 5 AND \"OperationRevision\" >= 1))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_outcome",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "(\"Status\" = 3 AND \"OutcomeCode\" IS NOT NULL AND \"HoldReviewAtUtc\" IS NOT NULL AND \"HoldReviewAtUtc\" >= \"LastChangedAtUtc\") OR (\"Status\" = 4 AND \"OutcomeCode\" IS NOT NULL AND \"HoldReviewAtUtc\" IS NULL) OR (\"Status\" IN (1, 2, 5) AND \"OutcomeCode\" IS NULL AND \"HoldReviewAtUtc\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_phase",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"Phase\" BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_status",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"Status\" BETWEEN 1 AND 5");
        }
    }
}
