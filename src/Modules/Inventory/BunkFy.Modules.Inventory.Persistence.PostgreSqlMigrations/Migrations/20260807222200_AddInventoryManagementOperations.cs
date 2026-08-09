using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryManagementOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_tenant_destroy_operation_progress",
                schema: "inventory",
                table: "tenant_destroy_operations");

            migrationBuilder.CreateTable(
                name: "management_operations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResourceKind = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultSalesMode = table.Column<int>(type: "integer", nullable: false),
                    ResultVersion = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_operations", x => new { x.ScopeId, x.ResourceKind, x.ResourceId, x.Id });
                    table.CheckConstraint("CK_inventory_management_operations_coordinates", "\"Id\" <> '00000000-0000-0000-0000-000000000000' AND \"PropertyId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResourceId\" <> '00000000-0000-0000-0000-000000000000' AND char_length(btrim(\"ScopeId\")) > 0");
                    table.CheckConstraint("CK_inventory_management_operations_fingerprint", "char_length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_inventory_management_operations_kind", "\"ResourceKind\" = 1 AND \"Kind\" = 1");
                    table.CheckConstraint("CK_inventory_management_operations_result", "\"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" - \"ExpectedVersion\" <= 1 AND \"ResultSalesMode\" IN (2, 3)");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_tenant_destroy_operation_progress",
                schema: "inventory",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 20 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_management_operations_ScopeId_PropertyId_CompletedAtUtc_Res~",
                schema: "inventory",
                table: "management_operations",
                columns: new[] { "ScopeId", "PropertyId", "CompletedAtUtc", "ResourceKind", "ResourceId", "Id" });
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
                        FROM inventory.management_operations) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade Inventory while management operation receipts exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "management_operations",
                schema: "inventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_tenant_destroy_operation_progress",
                schema: "inventory",
                table: "tenant_destroy_operations");

            migrationBuilder.Sql(
                """
                UPDATE inventory.tenant_destroy_operations
                SET "Stage" = 13
                WHERE "Stage" = 20;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_tenant_destroy_operation_progress",
                schema: "inventory",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 19 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
        }
    }
}
