namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations;

using BunkFy.Modules.Reservations.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(ReservationsDbContext))]
[Migration("20260715113114_AddExpectedReservationTimes")]
public partial class AddExpectedReservationTimes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<TimeOnly>(
            name: "ExpectedArrivalTime",
            schema: "reservations",
            table: "reservations",
            type: "time(0) without time zone",
            nullable: true);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "ExpectedDepartureTime",
            schema: "reservations",
            table: "reservations",
            type: "time(0) without time zone",
            nullable: true);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "PendingExpectedArrivalTime",
            schema: "reservations",
            table: "reservations",
            type: "time(0) without time zone",
            nullable: true);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "PendingExpectedDepartureTime",
            schema: "reservations",
            table: "reservations",
            type: "time(0) without time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExpectedArrivalTime",
            schema: "reservations",
            table: "reservations");

        migrationBuilder.DropColumn(
            name: "ExpectedDepartureTime",
            schema: "reservations",
            table: "reservations");

        migrationBuilder.DropColumn(
            name: "PendingExpectedArrivalTime",
            schema: "reservations",
            table: "reservations");

        migrationBuilder.DropColumn(
            name: "PendingExpectedDepartureTime",
            schema: "reservations",
            table: "reservations");
    }
}
