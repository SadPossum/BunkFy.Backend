using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestRestrictionEligibilityProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guest_processing_restriction_projection",
                schema: "reservations",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    IsRestricted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_processing_restriction_projection", x => new { x.ScopeId, x.PropertyId, x.GuestId });
                    table.CheckConstraint("CK_reservations_guest_restriction_contract_version", "\"ContractVersion\" >= 1");
                    table.CheckConstraint("CK_reservations_guest_restriction_revision", "\"Revision\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_guest_restriction_linkable",
                schema: "reservations",
                table: "guest_processing_restriction_projection",
                columns: new[] { "ScopeId", "PropertyId", "IsRestricted", "GuestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guest_processing_restriction_projection",
                schema: "reservations");
        }
    }
}
