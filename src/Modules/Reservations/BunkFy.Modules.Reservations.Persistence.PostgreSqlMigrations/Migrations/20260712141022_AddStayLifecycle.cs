using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStayLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckedInAtUtc",
                schema: "reservations",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CheckedInBusinessDate",
                schema: "reservations",
                table: "reservations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckedInBy",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckedOutAtUtc",
                schema: "reservations",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CheckedOutBusinessDate",
                schema: "reservations",
                table: "reservations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckedOutBy",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NoShowAtUtc",
                schema: "reservations",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NoShowBusinessDate",
                schema: "reservations",
                table: "reservations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoShowBy",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingStayActorId",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PendingStayBusinessDate",
                schema: "reservations",
                table: "reservations",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_checked_in_complete",
                schema: "reservations",
                table: "reservations",
                sql: "(\"Status\" IN (6, 9, 10) AND \"CheckedInBusinessDate\" IS NOT NULL AND \"CheckedInAtUtc\" IS NOT NULL AND \"CheckedInBy\" IS NOT NULL AND length(trim(\"CheckedInBy\")) > 0) OR (\"Status\" NOT IN (6, 9, 10) AND \"CheckedInBusinessDate\" IS NULL AND \"CheckedInAtUtc\" IS NULL AND \"CheckedInBy\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_checked_out_complete",
                schema: "reservations",
                table: "reservations",
                sql: "(\"Status\" = 10 AND \"CheckedOutBusinessDate\" IS NOT NULL AND \"CheckedOutAtUtc\" IS NOT NULL AND \"CheckedOutBy\" IS NOT NULL AND length(trim(\"CheckedOutBy\")) > 0) OR (\"Status\" <> 10 AND \"CheckedOutBusinessDate\" IS NULL AND \"CheckedOutAtUtc\" IS NULL AND \"CheckedOutBy\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_no_show_complete",
                schema: "reservations",
                table: "reservations",
                sql: "(\"Status\" = 8 AND \"NoShowBusinessDate\" IS NOT NULL AND \"NoShowAtUtc\" IS NOT NULL AND \"NoShowBy\" IS NOT NULL AND length(trim(\"NoShowBy\")) > 0) OR (\"Status\" <> 8 AND \"NoShowBusinessDate\" IS NULL AND \"NoShowAtUtc\" IS NULL AND \"NoShowBy\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_pending_stay_complete",
                schema: "reservations",
                table: "reservations",
                sql: "(\"Status\" IN (7, 9) AND \"PendingStayBusinessDate\" IS NOT NULL AND \"PendingStayActorId\" IS NOT NULL AND length(trim(\"PendingStayActorId\")) > 0 AND \"ReleaseRequestId\" IS NOT NULL) OR (\"Status\" NOT IN (7, 9) AND \"PendingStayBusinessDate\" IS NULL AND \"PendingStayActorId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_checked_in_complete",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_checked_out_complete",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_no_show_complete",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_pending_stay_complete",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "CheckedInAtUtc",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "CheckedInBusinessDate",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "CheckedInBy",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "CheckedOutAtUtc",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "CheckedOutBusinessDate",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "CheckedOutBy",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "NoShowAtUtc",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "NoShowBusinessDate",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "NoShowBy",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingStayActorId",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "PendingStayBusinessDate",
                schema: "reservations",
                table: "reservations");
        }
    }
}
