using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentParticipantState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stay_history_ScopeId_PropertyId_GuestId_Arrival",
                schema: "guests",
                table: "stay_history");

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentParticipant",
                schema: "guests",
                table: "stay_history",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_stay_history_ScopeId_PropertyId_IsCurrentParticipant_GuestI~",
                schema: "guests",
                table: "stay_history",
                columns: new[] { "ScopeId", "PropertyId", "IsCurrentParticipant", "GuestId", "Arrival" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stay_history_ScopeId_PropertyId_IsCurrentParticipant_GuestI~",
                schema: "guests",
                table: "stay_history");

            migrationBuilder.DropColumn(
                name: "IsCurrentParticipant",
                schema: "guests",
                table: "stay_history");

            migrationBuilder.CreateIndex(
                name: "IX_stay_history_ScopeId_PropertyId_GuestId_Arrival",
                schema: "guests",
                table: "stay_history",
                columns: new[] { "ScopeId", "PropertyId", "GuestId", "Arrival" });
        }
    }
}
