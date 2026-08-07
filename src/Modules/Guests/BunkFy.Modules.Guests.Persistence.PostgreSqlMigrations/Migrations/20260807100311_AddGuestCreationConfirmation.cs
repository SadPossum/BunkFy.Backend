using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestCreationConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreationConfirmationId",
                schema: "guests",
                table: "guest_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_profiles_ScopeId_CreationConfirmationId",
                schema: "guests",
                table: "guest_profiles",
                columns: new[] { "ScopeId", "CreationConfirmationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_guest_profiles_ScopeId_CreationConfirmationId",
                schema: "guests",
                table: "guest_profiles");

            migrationBuilder.DropColumn(
                name: "CreationConfirmationId",
                schema: "guests",
                table: "guest_profiles");
        }
    }
}
