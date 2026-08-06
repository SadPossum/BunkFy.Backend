using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Properties.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertiesOperationLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_tenant_destroy_operation_progress",
                schema: "properties",
                table: "tenant_destroy_operations");

            migrationBuilder.Sql(
                """
                UPDATE properties.tenant_destroy_operations
                SET "Stage" = 10
                WHERE "Stage" = 8;
                """);

            migrationBuilder.CreateTable(
                name: "property_operation_locks",
                schema: "properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_operation_locks", x => x.Id);
                    table.UniqueConstraint("AK_property_operation_locks_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_property_operation_locks_coordinate", "\"Id\" = \"PropertyId\"");
                    table.CheckConstraint("CK_property_operation_locks_revision", "\"Revision\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "room_operation_locks",
                schema: "properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_operation_locks", x => x.Id);
                    table.UniqueConstraint("AK_room_operation_locks_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_room_operation_locks_coordinate", "\"Id\" = \"RoomId\"");
                    table.CheckConstraint("CK_room_operation_locks_revision", "\"Revision\" >= 1");
                });

            migrationBuilder.Sql(
                """
                INSERT INTO properties.property_operation_locks
                    ("Id", "PropertyId", "Revision", "ScopeId")
                SELECT property."Id", property."Id", 1, property."ScopeId"
                FROM properties.properties AS property
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO properties.room_operation_locks
                    ("Id", "RoomId", "Revision", "ScopeId")
                SELECT room."Id", room."Id", 1, room."ScopeId"
                FROM properties.rooms AS room
                ON CONFLICT ("Id") DO NOTHING;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_tenant_destroy_operation_progress",
                schema: "properties",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 10 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_property_operation_locks_ScopeId_PropertyId",
                schema: "properties",
                table: "property_operation_locks",
                columns: new[] { "ScopeId", "PropertyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_room_operation_locks_ScopeId_RoomId",
                schema: "properties",
                table: "room_operation_locks",
                columns: new[] { "ScopeId", "RoomId" },
                unique: true);
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
                        FROM properties.tenant_destroy_operations
                        WHERE "Stage" IN (8, 9)
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade Properties while operation-lock cleanup is in progress.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "property_operation_locks",
                schema: "properties");

            migrationBuilder.DropTable(
                name: "room_operation_locks",
                schema: "properties");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_tenant_destroy_operation_progress",
                schema: "properties",
                table: "tenant_destroy_operations");

            migrationBuilder.Sql(
                """
                UPDATE properties.tenant_destroy_operations
                SET "Stage" = 8
                WHERE "Stage" = 10;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_tenant_destroy_operation_progress",
                schema: "properties",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 8 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
        }
    }
}
