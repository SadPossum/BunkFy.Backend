using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestLinkAuditState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reservation_guests_ScopeId_ReservationId_Role",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                schema: "reservations",
                table: "reservation_guests",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "LinkVersion",
                schema: "reservations",
                table: "reservation_guests",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UnlinkedAtUtc",
                schema: "reservations",
                table: "reservation_guests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlinkedBy",
                schema: "reservations",
                table: "reservation_guests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_guests_ScopeId_ReservationId_IsCurrent_Role",
                schema: "reservations",
                table: "reservation_guests",
                columns: new[] { "ScopeId", "ReservationId", "IsCurrent", "Role" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reservation_guests_ScopeId_ReservationId_IsCurrent_Role",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "LinkVersion",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "UnlinkedAtUtc",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.DropColumn(
                name: "UnlinkedBy",
                schema: "reservations",
                table: "reservation_guests");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_guests_ScopeId_ReservationId_Role",
                schema: "reservations",
                table: "reservation_guests",
                columns: new[] { "ScopeId", "ReservationId", "Role" },
                unique: true);
        }
    }
}
