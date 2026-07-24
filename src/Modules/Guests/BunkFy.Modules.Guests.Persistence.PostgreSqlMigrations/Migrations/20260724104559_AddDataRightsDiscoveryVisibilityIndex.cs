using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDataRightsDiscoveryVisibilityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_stay_history_ScopeId_PropertyId_GuestId",
                schema: "guests",
                table: "stay_history",
                columns: new[] { "ScopeId", "PropertyId", "GuestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stay_history_ScopeId_PropertyId_GuestId",
                schema: "guests",
                table: "stay_history");
        }
    }
}
