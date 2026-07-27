using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class SupportTenantScopedStaffRightsCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_kind",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_property_scope",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_requester_scope",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_kind",
                schema: "data-rights",
                table: "cases",
                sql: "\"Kind\" IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" <> 3 AND \"RequestedOperations\" BETWEEN 1 AND 31) OR (\"Kind\" = 3 AND \"RequestedOperations\" = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_property_scope",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" = 1 AND \"PropertyId\" IS NOT NULL) OR (\"Kind\" IN (2, 3) AND \"PropertyId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_requester_scope",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" IN (1, 3) AND \"RequesterRelationship\" IN (1, 2, 3)) OR (\"Kind\" = 2 AND \"RequesterRelationship\" IN (3, 4))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_kind",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_property_scope",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_requester_scope",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_kind",
                schema: "data-rights",
                table: "cases",
                sql: "\"Kind\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases",
                sql: "\"RequestedOperations\" BETWEEN 1 AND 31");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_property_scope",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" = 1 AND \"PropertyId\" IS NOT NULL) OR (\"Kind\" = 2 AND \"PropertyId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_requester_scope",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" = 1 AND \"RequesterRelationship\" IN (1, 2, 3)) OR (\"Kind\" = 2 AND \"RequesterRelationship\" IN (3, 4))");
        }
    }
}
