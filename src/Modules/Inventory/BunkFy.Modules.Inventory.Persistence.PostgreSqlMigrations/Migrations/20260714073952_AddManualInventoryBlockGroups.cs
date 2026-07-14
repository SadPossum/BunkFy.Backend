using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddManualInventoryBlockGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BlockGroupId",
                schema: "inventory",
                table: "manual_blocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE inventory.manual_blocks SET \"BlockGroupId\" = \"Id\" WHERE \"BlockGroupId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "BlockGroupId",
                schema: "inventory",
                table: "manual_blocks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_blocks_ScopeId_PropertyId_BlockGroupId_Status",
                schema: "inventory",
                table: "manual_blocks",
                columns: new[] { "ScopeId", "PropertyId", "BlockGroupId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_manual_blocks_ScopeId_PropertyId_BlockGroupId_Status",
                schema: "inventory",
                table: "manual_blocks");

            migrationBuilder.DropColumn(
                name: "BlockGroupId",
                schema: "inventory",
                table: "manual_blocks");
        }
    }
}
