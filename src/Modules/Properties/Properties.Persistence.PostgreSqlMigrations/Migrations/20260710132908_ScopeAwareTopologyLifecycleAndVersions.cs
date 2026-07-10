using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Properties.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ScopeAwareTopologyLifecycleAndVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rooms_properties_TenantId_PropertyId",
                schema: "properties",
                table: "rooms");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_properties_TenantId_Id",
                schema: "properties",
                table: "properties");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "properties",
                table: "rooms",
                newName: "ScopeId");

            migrationBuilder.RenameIndex(
                name: "IX_rooms_TenantId_PropertyId_Name",
                schema: "properties",
                table: "rooms",
                newName: "IX_rooms_ScopeId_PropertyId_Name");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "properties",
                table: "properties",
                newName: "ScopeId");

            migrationBuilder.RenameIndex(
                name: "IX_properties_TenantId_Code",
                schema: "properties",
                table: "properties",
                newName: "IX_properties_ScopeId_Code");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "properties",
                table: "outbox_messages",
                newName: "ScopeId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "properties",
                table: "beds",
                newName: "ScopeId");

            migrationBuilder.RenameIndex(
                name: "IX_beds_TenantId_RoomId_Label",
                schema: "properties",
                table: "beds",
                newName: "IX_beds_ScopeId_RoomId_Label");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "properties",
                table: "rooms",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "ProjectionOrdinal",
                schema: "properties",
                table: "properties",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetiredAtUtc",
                schema: "properties",
                table: "properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "properties",
                table: "properties",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "properties",
                table: "beds",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_properties_ScopeId_Id",
                schema: "properties",
                table: "properties",
                columns: new[] { "ScopeId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_properties_ProjectionOrdinal",
                schema: "properties",
                table: "properties",
                column: "ProjectionOrdinal",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_properties_ScopeId_PropertyId",
                schema: "properties",
                table: "rooms",
                columns: new[] { "ScopeId", "PropertyId" },
                principalSchema: "properties",
                principalTable: "properties",
                principalColumns: new[] { "ScopeId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rooms_properties_ScopeId_PropertyId",
                schema: "properties",
                table: "rooms");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_properties_ScopeId_Id",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropIndex(
                name: "IX_properties_ProjectionOrdinal",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "properties",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "ProjectionOrdinal",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "RetiredAtUtc",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "properties",
                table: "beds");

            migrationBuilder.RenameColumn(
                name: "ScopeId",
                schema: "properties",
                table: "rooms",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_rooms_ScopeId_PropertyId_Name",
                schema: "properties",
                table: "rooms",
                newName: "IX_rooms_TenantId_PropertyId_Name");

            migrationBuilder.RenameColumn(
                name: "ScopeId",
                schema: "properties",
                table: "properties",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_properties_ScopeId_Code",
                schema: "properties",
                table: "properties",
                newName: "IX_properties_TenantId_Code");

            migrationBuilder.RenameColumn(
                name: "ScopeId",
                schema: "properties",
                table: "outbox_messages",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "ScopeId",
                schema: "properties",
                table: "beds",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_beds_ScopeId_RoomId_Label",
                schema: "properties",
                table: "beds",
                newName: "IX_beds_TenantId_RoomId_Label");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_properties_TenantId_Id",
                schema: "properties",
                table: "properties",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_properties_TenantId_PropertyId",
                schema: "properties",
                table: "rooms",
                columns: new[] { "TenantId", "PropertyId" },
                principalSchema: "properties",
                principalTable: "properties",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
