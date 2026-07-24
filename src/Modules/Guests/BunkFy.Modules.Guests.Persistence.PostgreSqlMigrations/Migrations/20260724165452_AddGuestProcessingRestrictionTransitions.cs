using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestProcessingRestrictionTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_guest_processing_restrictions_ScopeId_PropertyId_GuestId_Re~",
                schema: "guests",
                table: "guest_processing_restrictions",
                columns: new[] { "ScopeId", "PropertyId", "GuestId", "ReleaseCaseId", "ReleaseApprovalRevision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_guest_processing_restrictions_ScopeId_PropertyId_GuestId_Re~",
                schema: "guests",
                table: "guest_processing_restrictions");
        }
    }
}
