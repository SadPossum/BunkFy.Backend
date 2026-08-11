using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryRetirementCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_room_retirements_ScopeId_RoomId",
                schema: "inventory",
                table: "room_retirements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_management_operations_kind",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_management_operations_result",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropIndex(
                name: "IX_bed_retirements_ScopeId_BedId",
                schema: "inventory",
                table: "bed_retirements");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CanceledAtUtc",
                schema: "inventory",
                table: "room_retirements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceledBy",
                schema: "inventory",
                table: "room_retirements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "inventory",
                table: "room_retirements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CanceledAtUtc",
                schema: "inventory",
                table: "bed_retirements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceledBy",
                schema: "inventory",
                table: "bed_retirements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "inventory",
                table: "bed_retirements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_room_retirements_ScopeId_RoomId_State",
                schema: "inventory",
                table: "room_retirements",
                columns: new[] { "ScopeId", "RoomId", "State" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_management_operations_kind",
                schema: "inventory",
                table: "management_operations",
                sql: "\"Kind\" BETWEEN 1 AND 11 AND ((\"Kind\" = 1 AND \"ResourceKind\" = 1) OR (\"Kind\" IN (2, 3) AND \"ResourceKind\" = 2 AND \"ResourceId\" = \"PropertyId\") OR (\"Kind\" = 4 AND \"ResourceKind\" = 3 AND \"ResultBlockId\" IS NOT NULL AND \"ResourceId\" = \"ResultBlockId\") OR (\"Kind\" = 5 AND \"ResourceKind\" = 4 AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResourceId\" = \"ResultBlockGroupId\") OR (\"Kind\" = 6 AND \"ResourceKind\" = 5) OR (\"Kind\" = 7 AND \"ResourceKind\" = 6 AND \"ResourceId\" = \"ResultTopologyChangeId\") OR (\"Kind\" = 8 AND \"ResourceKind\" = 1) OR (\"Kind\" = 9 AND \"ResourceKind\" = 7 AND \"ResourceId\" = \"ResultTopologyChangeId\") OR (\"Kind\" = 10 AND \"ResourceKind\" = 6 AND \"ResourceId\" = \"ResultTopologyChangeId\") OR (\"Kind\" = 11 AND \"ResourceKind\" = 7 AND \"ResourceId\" = \"ResultTopologyChangeId\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_management_operations_result",
                schema: "inventory",
                table: "management_operations",
                sql: "(\"Kind\" = 1 AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" BETWEEN \"ExpectedVersion\" AND \"ExpectedVersion\" + 1 AND \"ResultSalesMode\" IS NOT NULL AND \"ResultSalesMode\" IN (2, 3) AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NULL AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NULL AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" = 2 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 1 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NOT NULL AND \"ResultBlockId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NOT NULL AND \"ResultBlockStatus\" = 1 AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" = 1 AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" = 3 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 0 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" > 0 AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" = 4 AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" = \"ExpectedVersion\" + 1 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NOT NULL AND \"ResultBlockId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NOT NULL AND \"ResultBlockStatus\" = 2 AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" = 1 AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" = 5 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 0 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" > 0 AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" IN (6, 8) AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" > 0 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NULL AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NULL AND \"ResultTopologyChangeId\" IS NOT NULL AND \"ResultTopologyChangeId\" <> '00000000-0000-0000-0000-000000000000') OR (\"Kind\" IN (7, 9, 10, 11) AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" = \"ExpectedVersion\" + 1 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NULL AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NULL AND \"ResultTopologyChangeId\" IS NOT NULL AND \"ResultTopologyChangeId\" <> '00000000-0000-0000-0000-000000000000')");

            migrationBuilder.CreateIndex(
                name: "IX_bed_retirements_ScopeId_BedId_State",
                schema: "inventory",
                table: "bed_retirements",
                columns: new[] { "ScopeId", "BedId", "State" });

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_bed_retirements_ScopeId_BedId_active"
                    ON inventory.bed_retirements ("ScopeId", "BedId")
                    WHERE "State" IN (1, 2, 3, 5);

                CREATE UNIQUE INDEX "UX_room_retirements_ScopeId_RoomId_active"
                    ON inventory.room_retirements ("ScopeId", "RoomId")
                    WHERE "State" IN (1, 2, 3, 5);
                """);
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
                        WHERE "Kind" IN (10, 11)) OR
                       EXISTS (
                        SELECT 1
                        FROM inventory.bed_retirements
                        WHERE "State" = 6 OR
                            "CancellationReason" IS NOT NULL OR
                            "CanceledBy" IS NOT NULL OR
                            "CanceledAtUtc" IS NOT NULL) OR
                       EXISTS (
                        SELECT 1
                        FROM inventory.room_retirements
                        WHERE "State" = 6 OR
                            "CancellationReason" IS NOT NULL OR
                            "CanceledBy" IS NOT NULL OR
                            "CanceledAtUtc" IS NOT NULL) OR
                       EXISTS (
                        SELECT 1
                        FROM inventory.bed_retirements
                        GROUP BY "ScopeId", "BedId"
                        HAVING COUNT(*) > 1) OR
                       EXISTS (
                        SELECT 1
                        FROM inventory.room_retirements
                        GROUP BY "ScopeId", "RoomId"
                        HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade Inventory while retirement cancellation history exists.';
                    END IF;
                END $$;

                DROP INDEX inventory."UX_bed_retirements_ScopeId_BedId_active";
                DROP INDEX inventory."UX_room_retirements_ScopeId_RoomId_active";
                """);

            migrationBuilder.DropIndex(
                name: "IX_room_retirements_ScopeId_RoomId_State",
                schema: "inventory",
                table: "room_retirements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_management_operations_kind",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_management_operations_result",
                schema: "inventory",
                table: "management_operations");

            migrationBuilder.DropIndex(
                name: "IX_bed_retirements_ScopeId_BedId_State",
                schema: "inventory",
                table: "bed_retirements");

            migrationBuilder.DropColumn(
                name: "CanceledAtUtc",
                schema: "inventory",
                table: "room_retirements");

            migrationBuilder.DropColumn(
                name: "CanceledBy",
                schema: "inventory",
                table: "room_retirements");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "inventory",
                table: "room_retirements");

            migrationBuilder.DropColumn(
                name: "CanceledAtUtc",
                schema: "inventory",
                table: "bed_retirements");

            migrationBuilder.DropColumn(
                name: "CanceledBy",
                schema: "inventory",
                table: "bed_retirements");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "inventory",
                table: "bed_retirements");

            migrationBuilder.CreateIndex(
                name: "IX_room_retirements_ScopeId_RoomId",
                schema: "inventory",
                table: "room_retirements",
                columns: new[] { "ScopeId", "RoomId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_management_operations_kind",
                schema: "inventory",
                table: "management_operations",
                sql: "\"Kind\" BETWEEN 1 AND 9 AND ((\"Kind\" = 1 AND \"ResourceKind\" = 1) OR (\"Kind\" IN (2, 3) AND \"ResourceKind\" = 2 AND \"ResourceId\" = \"PropertyId\") OR (\"Kind\" = 4 AND \"ResourceKind\" = 3 AND \"ResultBlockId\" IS NOT NULL AND \"ResourceId\" = \"ResultBlockId\") OR (\"Kind\" = 5 AND \"ResourceKind\" = 4 AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResourceId\" = \"ResultBlockGroupId\") OR (\"Kind\" = 6 AND \"ResourceKind\" = 5) OR (\"Kind\" = 7 AND \"ResourceKind\" = 6 AND \"ResourceId\" = \"ResultTopologyChangeId\") OR (\"Kind\" = 8 AND \"ResourceKind\" = 1) OR (\"Kind\" = 9 AND \"ResourceKind\" = 7 AND \"ResourceId\" = \"ResultTopologyChangeId\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_management_operations_result",
                schema: "inventory",
                table: "management_operations",
                sql: "(\"Kind\" = 1 AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" BETWEEN \"ExpectedVersion\" AND \"ExpectedVersion\" + 1 AND \"ResultSalesMode\" IS NOT NULL AND \"ResultSalesMode\" IN (2, 3) AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NULL AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NULL AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" = 2 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 1 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NOT NULL AND \"ResultBlockId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NOT NULL AND \"ResultBlockStatus\" = 1 AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" = 1 AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" = 3 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 0 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" > 0 AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" = 4 AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" = \"ExpectedVersion\" + 1 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NOT NULL AND \"ResultBlockId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NOT NULL AND \"ResultBlockStatus\" = 2 AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" = 1 AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" = 5 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 0 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NOT NULL AND \"ResultBlockGroupId\" <> '00000000-0000-0000-0000-000000000000' AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NOT NULL AND \"ResultAffectedBlockCount\" > 0 AND \"ResultTopologyChangeId\" IS NULL) OR (\"Kind\" IN (6, 8) AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" > 0 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NULL AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NULL AND \"ResultTopologyChangeId\" IS NOT NULL AND \"ResultTopologyChangeId\" <> '00000000-0000-0000-0000-000000000000') OR (\"Kind\" IN (7, 9) AND \"ExpectedVersion\" > 0 AND \"ResultVersion\" = \"ExpectedVersion\" + 1 AND \"ResultSalesMode\" IS NULL AND \"ResultBlockId\" IS NULL AND \"ResultBlockGroupId\" IS NULL AND \"ResultBlockStatus\" IS NULL AND \"ResultAffectedBlockCount\" IS NULL AND \"ResultTopologyChangeId\" IS NOT NULL AND \"ResultTopologyChangeId\" <> '00000000-0000-0000-0000-000000000000')");

            migrationBuilder.CreateIndex(
                name: "IX_bed_retirements_ScopeId_BedId",
                schema: "inventory",
                table: "bed_retirements",
                columns: new[] { "ScopeId", "BedId" },
                unique: true);
        }
    }
}
