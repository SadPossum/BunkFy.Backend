using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomRetirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_retirements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_room_retirements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_room_retirements_ScopeId_PropertyId_State",
                schema: "inventory",
                table: "room_retirements",
                columns: new[] { "ScopeId", "PropertyId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_room_retirements_ScopeId_RoomId",
                schema: "inventory",
                table: "room_retirements",
                columns: new[] { "ScopeId", "RoomId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_retirements",
                schema: "inventory");
        }
    }
}
