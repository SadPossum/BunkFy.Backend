using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Properties.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddBedMutationOperationReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_resource",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_status",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_versions",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.AddColumn<int>(
                name: "ResultAffectedBedCount",
                schema: "properties",
                table: "property_mutation_operations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResultBedId",
                schema: "properties",
                table: "property_mutation_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultBedStatus",
                schema: "properties",
                table: "property_mutation_operations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4, 5, 6, 7, 8, 9)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_resource",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "(\"Kind\" IN (1, 2, 3, 4, 5) AND \"ResourceKind\" = 1 AND \"ResourceId\" = \"PropertyId\") OR (\"Kind\" IN (6, 7, 8, 9) AND \"ResourceKind\" = 2 AND \"ResourceId\" = \"ResultRoomId\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_status",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "(\"Kind\" IN (1, 2, 3, 4) AND \"ResultStatus\" IN (1, 2) AND \"ResultProcessingStatus\" IN (1, 2, 3) AND \"ResultRoomId\" IS NULL AND \"ResultRoomStatus\" IS NULL AND \"ResultBedId\" IS NULL AND \"ResultBedStatus\" IS NULL AND \"ResultAffectedBedCount\" IS NULL) OR (\"Kind\" IN (5, 6) AND \"ResultStatus\" IS NULL AND \"ResultProcessingStatus\" IS NULL AND \"ResultRoomId\" IS NOT NULL AND \"ResultRoomStatus\" IN (1, 2) AND \"ResultBedId\" IS NULL AND \"ResultBedStatus\" IS NULL AND \"ResultAffectedBedCount\" IS NULL) OR (\"Kind\" IN (7, 9) AND \"ResultStatus\" IS NULL AND \"ResultProcessingStatus\" IS NULL AND \"ResultRoomId\" IS NOT NULL AND \"ResultRoomStatus\" IS NULL AND \"ResultBedId\" IS NOT NULL AND \"ResultBedStatus\" = 1 AND \"ResultAffectedBedCount\" IS NULL) OR (\"Kind\" = 8 AND \"ResultStatus\" IS NULL AND \"ResultProcessingStatus\" IS NULL AND \"ResultRoomId\" IS NOT NULL AND \"ResultRoomStatus\" IS NULL AND \"ResultBedId\" IS NULL AND \"ResultBedStatus\" IS NULL AND \"ResultAffectedBedCount\" BETWEEN 1 AND 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_versions",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"ExpectedVersion\" > 0 AND \"ResultVersion\" > 0 AND ((\"Kind\" IN (1, 2, 3, 4, 6, 9) AND \"ResultResourceVersion\" >= \"ExpectedVersion\" AND \"ResultResourceVersion\" <= \"ExpectedVersion\" + 1) OR (\"Kind\" IN (5, 7) AND \"ResultResourceVersion\" = \"ExpectedVersion\" + 1) OR (\"Kind\" = 8 AND \"ResultAffectedBedCount\" BETWEEN 1 AND 100 AND \"ResultResourceVersion\" = \"ExpectedVersion\" + \"ResultAffectedBedCount\" AND \"ResultVersion\" = \"ResultResourceVersion\"))");
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
                        FROM properties.property_mutation_operations
                        WHERE "Kind" IN (7, 8, 9)) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade Properties while bed mutation operation receipts exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_resource",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_status",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_versions",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropColumn(
                name: "ResultAffectedBedCount",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropColumn(
                name: "ResultBedId",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropColumn(
                name: "ResultBedStatus",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4, 5, 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_resource",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "(\"Kind\" IN (1, 2, 3, 4, 5) AND \"ResourceKind\" = 1 AND \"ResourceId\" = \"PropertyId\") OR (\"Kind\" = 6 AND \"ResourceKind\" = 2 AND \"ResourceId\" = \"ResultRoomId\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_status",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "(\"Kind\" IN (1, 2, 3, 4) AND \"ResultStatus\" IN (1, 2) AND \"ResultProcessingStatus\" IN (1, 2, 3) AND \"ResultRoomId\" IS NULL AND \"ResultRoomStatus\" IS NULL) OR (\"Kind\" IN (5, 6) AND \"ResultStatus\" IS NULL AND \"ResultProcessingStatus\" IS NULL AND \"ResultRoomId\" IS NOT NULL AND \"ResultRoomStatus\" IN (1, 2))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_versions",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"ExpectedVersion\" > 0 AND \"ResultVersion\" > 0 AND \"ResultResourceVersion\" >= \"ExpectedVersion\" AND \"ResultResourceVersion\" <= \"ExpectedVersion\" + 1");
        }
    }
}
