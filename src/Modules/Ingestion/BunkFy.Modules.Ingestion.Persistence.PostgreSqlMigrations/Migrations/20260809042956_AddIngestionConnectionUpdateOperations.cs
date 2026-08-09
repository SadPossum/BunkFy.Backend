using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionConnectionUpdateOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_connection_management_operations_create",
                schema: "ingestion",
                table: "connection_management_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_connection_management_operations_outcome",
                schema: "ingestion",
                table: "connection_management_operations",
                sql: "(\"Kind\" = 1 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 1 AND \"Id\" = \"ConnectionId\") OR (\"Kind\" = 2 AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + 1)");
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
                        WHERE "Kind" = 2)
                    THEN
                        RAISE EXCEPTION 'Cannot remove Ingestion connection update operation support while update receipts exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_connection_management_operations_outcome",
                schema: "ingestion",
                table: "connection_management_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_connection_management_operations_create",
                schema: "ingestion",
                table: "connection_management_operations",
                sql: "\"Kind\" = 1 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 1 AND \"Id\" = \"ConnectionId\"");
        }
    }
}
