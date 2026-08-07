using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationGuestRecordLinkProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservation_guest_record_link_processes",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ReviewReason = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    DispatchRevision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_guest_record_link_processes", x => x.Id);
                    table.UniqueConstraint("AK_reservation_guest_record_link_processes_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_guest_record_link_processes_lifecycle", "(\"State\" = 1 AND \"ReviewReason\" = 1 AND \"DispatchRevision\" = 0 AND \"RequestedBy\" IS NOT NULL) OR (\"State\" = 2 AND \"ReviewReason\" = 1 AND \"DispatchRevision\" >= 1 AND \"RequestedBy\" IS NOT NULL) OR (\"State\" = 3 AND \"ReviewReason\" = 1 AND \"DispatchRevision\" >= 1 AND \"RequestedBy\" IS NULL) OR (\"State\" = 4 AND \"ReviewReason\" >= 2 AND \"DispatchRevision\" >= 0 AND \"RequestedBy\" IS NOT NULL)");
                    table.CheckConstraint("CK_reservation_guest_record_link_processes_timestamps", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_reservation_guest_record_link_processes_versions", "\"ExpectedReservationVersion\" >= 1 AND \"Revision\" >= 1 AND \"DispatchRevision\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_guest_record_link_processes_ScopeId_CreationCon~",
                schema: "reservations",
                table: "reservation_guest_record_link_processes",
                columns: new[] { "ScopeId", "CreationConfirmationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_guest_record_link_processes_ScopeId_PropertyId_~",
                schema: "reservations",
                table: "reservation_guest_record_link_processes",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_guest_record_link_processes_ScopeId_PropertyId~1",
                schema: "reservations",
                table: "reservation_guest_record_link_processes",
                columns: new[] { "ScopeId", "PropertyId", "State", "UpdatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_guest_record_link_processes_ScopeId_Reservation~",
                schema: "reservations",
                table: "reservation_guest_record_link_processes",
                columns: new[] { "ScopeId", "ReservationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_guest_record_link_processes",
                schema: "reservations");
        }
    }
}
