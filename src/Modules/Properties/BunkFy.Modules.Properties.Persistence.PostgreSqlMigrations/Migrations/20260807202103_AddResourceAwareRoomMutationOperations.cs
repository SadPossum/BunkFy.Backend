using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Properties.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceAwareRoomMutationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_property_mutation_operations",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropIndex(
                name: "IX_property_mutation_operations_ScopeId_CompletedAtUtc_Id",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_processing",
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

            migrationBuilder.AlterColumn<int>(
                name: "ResultStatus",
                schema: "properties",
                table: "property_mutation_operations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ResultProcessingStatus",
                schema: "properties",
                table: "property_mutation_operations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ResourceKind",
                schema: "properties",
                table: "property_mutation_operations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                schema: "properties",
                table: "property_mutation_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ResultResourceVersion",
                schema: "properties",
                table: "property_mutation_operations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResultRoomId",
                schema: "properties",
                table: "property_mutation_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultRoomStatus",
                schema: "properties",
                table: "property_mutation_operations",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE properties.property_mutation_operations
                SET "ResourceKind" = 1,
                    "ResourceId" = "PropertyId",
                    "ResultResourceVersion" = "ResultVersion";
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ResourceKind",
                schema: "properties",
                table: "property_mutation_operations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "properties",
                table: "property_mutation_operations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ResultResourceVersion",
                schema: "properties",
                table: "property_mutation_operations",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_property_mutation_operations",
                schema: "properties",
                table: "property_mutation_operations",
                columns: new[] { "ScopeId", "ResourceKind", "ResourceId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_property_mutation_operations_ScopeId_CompletedAtUtc_Resourc~",
                schema: "properties",
                table: "property_mutation_operations",
                columns: new[] { "ScopeId", "CompletedAtUtc", "ResourceKind", "ResourceId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_property_mutation_operations_ScopeId_PropertyId",
                schema: "properties",
                table: "property_mutation_operations",
                columns: new[] { "ScopeId", "PropertyId" });

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
                        WHERE "Kind" IN (5, 6)) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade Properties while room mutation operation receipts exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_property_mutation_operations",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropIndex(
                name: "IX_property_mutation_operations_ScopeId_CompletedAtUtc_Resourc~",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropIndex(
                name: "IX_property_mutation_operations_ScopeId_PropertyId",
                schema: "properties",
                table: "property_mutation_operations");

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
                name: "ResourceKind",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropColumn(
                name: "ResultResourceVersion",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropColumn(
                name: "ResultRoomId",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.DropColumn(
                name: "ResultRoomStatus",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.AlterColumn<int>(
                name: "ResultStatus",
                schema: "properties",
                table: "property_mutation_operations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ResultProcessingStatus",
                schema: "properties",
                table: "property_mutation_operations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_property_mutation_operations",
                schema: "properties",
                table: "property_mutation_operations",
                columns: new[] { "ScopeId", "PropertyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_property_mutation_operations_ScopeId_CompletedAtUtc_Id",
                schema: "properties",
                table: "property_mutation_operations",
                columns: new[] { "ScopeId", "CompletedAtUtc", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_processing",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"ResultProcessingStatus\" IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_status",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"ResultStatus\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_versions",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + 1");
        }
    }
}
