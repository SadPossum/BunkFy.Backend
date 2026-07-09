using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogRegionAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "item_available_regions",
                schema: "catalog",
                columns: table => new
                {
                    RegionCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_available_regions", x => new { x.CatalogItemId, x.RegionCode });
                    table.ForeignKey(
                        name: "FK_item_available_regions_items_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_available_regions_CatalogItemId_RegionCode",
                schema: "catalog",
                table: "item_available_regions",
                columns: new[] { "CatalogItemId", "RegionCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_available_regions",
                schema: "catalog");
        }
    }
}
