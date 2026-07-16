using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationNotificationActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingCancellationActorId",
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
                name: "PendingCancellationActorId",
                schema: "reservations",
                table: "reservations");
        }
    }
}
