using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionConnectionControlOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_connection_management_operations_outcome",
                schema: "ingestion",
                table: "connection_management_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_connection_management_operations_outcome",
                schema: "ingestion",
                table: "connection_management_operations",
                sql: "(\"Kind\" = 1 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 1 AND \"Id\" = \"ConnectionId\") OR (\"Kind\" IN (2, 5, 6, 7) AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + 1) OR (\"Kind\" IN (3, 4) AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" = \"ExpectedVersion\" + 1)");
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
                        FROM ingestion.connection_management_operations
                        WHERE "Kind" IN (3, 4, 5, 6, 7))
                    THEN
                        RAISE EXCEPTION 'Cannot remove Ingestion connection control operation support while control receipts exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_connection_management_operations_outcome",
                schema: "ingestion",
                table: "connection_management_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_connection_management_operations_outcome",
                schema: "ingestion",
                table: "connection_management_operations",
                sql: "(\"Kind\" = 1 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 1 AND \"Id\" = \"ConnectionId\") OR (\"Kind\" = 2 AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + 1)");
        }
    }
}
