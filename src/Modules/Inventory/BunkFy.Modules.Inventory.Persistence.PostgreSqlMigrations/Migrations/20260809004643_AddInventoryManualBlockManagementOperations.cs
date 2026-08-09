using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryManualBlockManagementOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_management_operations_kind",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_management_operations_result",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.AlterColumn<int>(
                name: "ResultSalesMode",
                schema: "inventory",
                table: "management_operations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ResultAffectedBlockCount",
                schema: "inventory",
                table: "management_operations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResultBlockGroupId",
                schema: "inventory",
                table: "management_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResultBlockId",
                schema: "inventory",
                table: "management_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultBlockStatus",
                schema: "inventory",
                table: "management_operations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_management_operations_kind",
                schema: "inventory",
                table: "management_operations",
                sql: "\"Kind\" BETWEEN 1 AND 5 AND ((\"Kind\" = 1 AND \"ResourceKind\" = 1) OR (\"Kind\" IN (2, 3) AND \"ResourceKind\" = 2 AND \"ResourceId\" = \"PropertyId\") OR (\"Kind\" = 4 AND \"ResourceKind\" = 3 AND \"ResultBlockId\" IS NOT NULL AND \"ResourceId\" = \"ResultBlockId\") OR (\"Kind\" = 5 AND \"ResourceKind\" = 4 AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResourceId\" = \"ResultBlockGroupId\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_management_operations_result",
                schema: "inventory",
                table: "management_operations",
                sql: "(\"Kind\" = 1 AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" BETWEEN \"ExpectedVersion\" AND \"ExpectedVersion\" + 1 AND \"ResultSalesMode\" IS NOT NULL AND \"ResultSalesMode\" IN (2, 3) AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NULL AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NULL) OR (\"Kind\" = 2 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 1 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NOT NULL AND \"ResultBlockId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NOT NULL AND \"ResultBlockStatus\" = 1 AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" = 1) OR (\"Kind\" = 3 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 0 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" > 0) OR (\"Kind\" = 4 AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" = \"ExpectedVersion\" + 1 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NOT NULL AND \"ResultBlockId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NOT NULL AND \"ResultBlockStatus\" = 2 AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" = 1) OR (\"Kind\" = 5 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 0 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" > 0)");
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
                        FROM inventory.management_operations
                        WHERE "Kind" <> 1) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade Inventory while manual block management operation receipts exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_management_operations_kind",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_management_operations_result",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropColumn(
                name: "ResultAffectedBlockCount",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropColumn(
                name: "ResultBlockGroupId",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropColumn(
                name: "ResultBlockId",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropColumn(
                name: "ResultBlockStatus",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.AlterColumn<int>(
                name: "ResultSalesMode",
                schema: "inventory",
                table: "management_operations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_management_operations_kind",
                schema: "inventory",
                table: "management_operations",
                sql: "\"ResourceKind\" = 1 AND \"Kind\" = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_management_operations_result",
                schema: "inventory",
                table: "management_operations",
                sql: "\"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" - \"ExpectedVersion\" <= 1 AND \"ResultSalesMode\" IN (2, 3)");
        }
    }
}
