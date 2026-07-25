using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDataHoldsAndEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservation_data_holds",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    PlacedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlacedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_data_holds", x => x.Id);
                    table.UniqueConstraint("AK_reservation_data_holds_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_data_holds_lifecycle", "(\"State\" = 1 AND \"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR (\"State\" = 2 AND \"ReleasedBy\" IS NOT NULL AND \"ReleasedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" >= \"PlacedAtUtc\" AND \"Version\" = 2)");
                    table.CheckConstraint("CK_reservation_data_holds_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "reservation_operation_locks",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_operation_locks", x => x.Id);
                    table.UniqueConstraint("AK_reservation_operation_locks_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_operation_locks_revision", "\"Revision\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "reservation_data_hold_receipts",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SelectedReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    SelectedDetailsRevision = table.Column<long>(type: "bigint", nullable: false),
                    ResultingHoldVersion = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_data_hold_receipts", x => x.Id);
                    table.UniqueConstraint("AK_reservation_data_hold_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_data_hold_receipts_versions", "\"SelectedReservationVersion\" >= 1 AND \"SelectedDetailsRevision\" >= 1 AND ((\"Action\" = 1 AND \"ResultingHoldVersion\" = 1) OR (\"Action\" = 2 AND \"ResultingHoldVersion\" = 2))");
                    table.ForeignKey(
                        name: "FK_reservation_data_hold_receipts_reservation_data_holds_Scope~",
                        columns: x => new { x.ScopeId, x.HoldId },
                        principalSchema: "reservations",
                        principalTable: "reservation_data_holds",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_hold_receipts_ScopeId_HoldId_Action",
                schema: "reservations",
                table: "reservation_data_hold_receipts",
                columns: new[] { "ScopeId", "HoldId", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_hold_receipts_ScopeId_IdempotencyKey",
                schema: "reservations",
                table: "reservation_data_hold_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_hold_receipts_ScopeId_PropertyId_Reservati~",
                schema: "reservations",
                table: "reservation_data_hold_receipts",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "HoldId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_holds_ScopeId_PropertyId_ReservationId_Pla~",
                schema: "reservations",
                table: "reservation_data_holds",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "PlacedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_holds_ScopeId_PropertyId_ReservationId_Sta~",
                schema: "reservations",
                table: "reservation_data_holds",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "State", "PlacedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_holds_ScopeId_ReservationId_State_Property~",
                schema: "reservations",
                table: "reservation_data_holds",
                columns: new[] { "ScopeId", "ReservationId", "State", "PropertyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_operation_locks_ScopeId_ReservationId",
                schema: "reservations",
                table: "reservation_operation_locks",
                columns: new[] { "ScopeId", "ReservationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_data_hold_receipts",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservation_operation_locks",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservation_data_holds",
                schema: "reservations");
        }
    }
}
