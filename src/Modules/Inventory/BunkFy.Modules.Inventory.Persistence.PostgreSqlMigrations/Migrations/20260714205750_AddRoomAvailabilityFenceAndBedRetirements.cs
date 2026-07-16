using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomAvailabilityFenceAndBedRetirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AvailabilityMutationVersion",
                schema: "inventory",
                table: "room_configurations",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "bed_retirements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    BedId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RejectionReasonCode = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bed_retirements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bed_retirements_ScopeId_BedId",
                schema: "inventory",
                table: "bed_retirements",
                columns: new[] { "ScopeId", "BedId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bed_retirements_ScopeId_PropertyId_RoomId_State",
                schema: "inventory",
                table: "bed_retirements",
                columns: new[] { "ScopeId", "PropertyId", "RoomId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bed_retirements",
                schema: "inventory");

            migrationBuilder.DropColumn(
                name: "AvailabilityMutationVersion",
                schema: "inventory",
                table: "room_configurations");
        }
    }
}
