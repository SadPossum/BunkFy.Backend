using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestStayHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stay_history",
                schema: "guests",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    Departure = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CheckedInBusinessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NoShowBusinessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CheckedOutBusinessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReservationVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_history", x => new { x.ScopeId, x.GuestId, x.ReservationId });
                    table.CheckConstraint("CK_guests_stay_history_range", "\"Arrival\" < \"Departure\"");
                    table.CheckConstraint("CK_guests_stay_history_version", "\"ReservationVersion\" >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_stay_history_ScopeId_PropertyId_GuestId_Arrival",
                schema: "guests",
                table: "stay_history",
                columns: new[] { "ScopeId", "PropertyId", "GuestId", "Arrival" });

            migrationBuilder.CreateIndex(
                name: "IX_stay_history_ScopeId_ReservationId_GuestId",
                schema: "guests",
                table: "stay_history",
                columns: new[] { "ScopeId", "ReservationId", "GuestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stay_history",
                schema: "guests");
        }
    }
}
