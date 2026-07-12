using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalGuestLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProjectionOrdinal",
                schema: "reservations",
                table: "reservations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateTable(
                name: "guest_profile_projection",
                schema: "reservations",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginPropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_profile_projection", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservations_guest_profile_projection_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "reservation_guests",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    LinkedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_guests", x => new { x.ScopeId, x.ReservationId, x.Id });
                    table.ForeignKey(
                        name: "FK_reservation_guests_reservations_ScopeId_ReservationId",
                        columns: x => new { x.ScopeId, x.ReservationId },
                        principalSchema: "reservations",
                        principalTable: "reservations",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ProjectionOrdinal",
                schema: "reservations",
                table: "reservations",
                column: "ProjectionOrdinal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_profile_projection_ScopeId_OriginPropertyId_Status_Id",
                schema: "reservations",
                table: "guest_profile_projection",
                columns: new[] { "ScopeId", "OriginPropertyId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_guests_ScopeId_Id_ReservationId",
                schema: "reservations",
                table: "reservation_guests",
                columns: new[] { "ScopeId", "Id", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_guests_ScopeId_ReservationId_Role",
                schema: "reservations",
                table: "reservation_guests",
                columns: new[] { "ScopeId", "ReservationId", "Role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guest_profile_projection",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservation_guests",
                schema: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_ProjectionOrdinal",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "ProjectionOrdinal",
                schema: "reservations",
                table: "reservations");
        }
    }
}
