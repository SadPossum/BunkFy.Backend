using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class VersionScopedAnonymisationOwnerProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_owner_contract",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_owner_contract",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "(\"CaseKind\" = 1 AND \"OwnerContractVersion\" = 1) OR (\"CaseKind\" = 3 AND \"OwnerContractVersion\" = 2)");
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
                        FROM "data-rights"."execution_work_items"
                        WHERE "CaseKind" = 3
                           OR "OwnerContractVersion" <> 1
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while scoped anonymisation owner work exists.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_owner_contract",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_owner_contract",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "\"OwnerContractVersion\" = 1");
        }
    }
}
