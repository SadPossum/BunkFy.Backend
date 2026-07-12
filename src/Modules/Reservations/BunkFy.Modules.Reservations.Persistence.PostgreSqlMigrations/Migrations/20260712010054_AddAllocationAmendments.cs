using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastAllocationAmendmentRejectionCode",
                schema: "reservations",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingAllocationAmendmentId",
                schema: "reservations",
                table: "reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingAllocationAmendmentRequestFingerprint",
                schema: "reservations",
                table: "reservations",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PendingArrival",
                schema: "reservations",
                table: "reservations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PendingDeparture",
                schema: "reservations",
                table: "reservations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingDetailsActorId",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingDetailsAdapterConnectionId",
                schema: "reservations",
                table: "reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingDetailsChangeOrigin",
                schema: "reservations",
                table: "reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingDetailsCorrelationId",
                schema: "reservations",
                table: "reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingDetailsExternalOperationId",
                schema: "reservations",
                table: "reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                schema: "reservations",
                table: "reservations",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingGuestCount",
                schema: "reservations",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingInventoryUnitIds",
                schema: "reservations",
                table: "reservations",
                type: "character varying(3300)",
                maxLength: 3300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingNotes",
                schema: "reservations",
                table: "reservations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPhone",
                schema: "reservations",
                table: "reservations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPrimaryGuestName",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastAllocationAmendmentRejectionCode",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingAllocationAmendmentId",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingAllocationAmendmentRequestFingerprint",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingArrival",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingDeparture",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingDetailsActorId",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingDetailsAdapterConnectionId",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingDetailsChangeOrigin",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingDetailsCorrelationId",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingDetailsExternalOperationId",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingEmail",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingGuestCount",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingInventoryUnitIds",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingNotes",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingPhone",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingPrimaryGuestName",
                schema: "reservations",
                table: "reservations");
        }
    }
}
