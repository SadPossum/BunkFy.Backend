using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AllowStaffRestrictionCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" <> 3 AND \"RequestedOperations\" BETWEEN 1 AND 31) OR (\"Kind\" = 3 AND \"RequestedOperations\" IN (1, 2, 4))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."cases"
                        WHERE "Kind" = 3 AND "RequestedOperations" = 4
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while Staff restriction cases exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" <> 3 AND \"RequestedOperations\" BETWEEN 1 AND 31) OR (\"Kind\" = 3 AND \"RequestedOperations\" IN (1, 2))");
        }
    }
}
