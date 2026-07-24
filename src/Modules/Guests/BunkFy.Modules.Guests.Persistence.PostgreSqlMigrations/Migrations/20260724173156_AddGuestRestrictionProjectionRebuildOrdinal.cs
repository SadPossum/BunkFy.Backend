using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestRestrictionProjectionRebuildOrdinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProjectionOrdinal",
                schema: "guests",
                table: "guest_processing_restriction_state",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "IX_guest_processing_restriction_state_ProjectionOrdinal",
                schema: "guests",
                table: "guest_processing_restriction_state",
                column: "ProjectionOrdinal",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_guest_processing_restriction_state_ProjectionOrdinal",
                schema: "guests",
                table: "guest_processing_restriction_state");

            migrationBuilder.DropColumn(
                name: "ProjectionOrdinal",
                schema: "guests",
                table: "guest_processing_restriction_state");
        }
    }
}
