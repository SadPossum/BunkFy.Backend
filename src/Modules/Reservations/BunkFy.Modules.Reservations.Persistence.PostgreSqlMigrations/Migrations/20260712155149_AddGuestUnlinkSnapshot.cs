using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestUnlinkSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "UnlinkedArrival",
                schema: "reservations",
                table: "reservation_guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UnlinkedCheckedInBusinessDate",
                schema: "reservations",
                table: "reservation_guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UnlinkedCheckedOutBusinessDate",
                schema: "reservations",
                table: "reservation_guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UnlinkedDeparture",
                schema: "reservations",
                table: "reservation_guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UnlinkedNoShowBusinessDate",
                schema: "reservations",
                table: "reservation_guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnlinkedReservationStatus",
                schema: "reservations",
                table: "reservation_guests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservation_guests_link_version",
                schema: "reservations",
                table: "reservation_guests",
                sql: "\"LinkVersion\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservation_guests_unlink_snapshot",
                schema: "reservations",
                table: "reservation_guests",
                sql: "(\"IsCurrent\" = TRUE AND \"UnlinkedBy\" IS NULL AND \"UnlinkedAtUtc\" IS NULL AND \"UnlinkedArrival\" IS NULL AND \"UnlinkedDeparture\" IS NULL AND \"UnlinkedReservationStatus\" IS NULL) OR (\"IsCurrent\" = FALSE AND \"UnlinkedBy\" IS NOT NULL AND \"UnlinkedAtUtc\" IS NOT NULL AND \"UnlinkedArrival\" IS NOT NULL AND \"UnlinkedDeparture\" IS NOT NULL AND \"UnlinkedReservationStatus\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_reservation_guests_link_version",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservation_guests_unlink_snapshot",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "UnlinkedArrival",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "UnlinkedCheckedInBusinessDate",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "UnlinkedCheckedOutBusinessDate",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "UnlinkedDeparture",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "UnlinkedNoShowBusinessDate",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "UnlinkedReservationStatus",
                schema: "reservations",
                table: "reservation_guests");
        }
    }
}
