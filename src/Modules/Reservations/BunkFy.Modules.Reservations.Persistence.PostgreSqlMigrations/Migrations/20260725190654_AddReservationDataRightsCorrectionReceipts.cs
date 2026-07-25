using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDataRightsCorrectionReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservation_data_rights_correction_receipts",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    CurrentRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    SelectedDetailsRevision = table.Column<long>(type: "bigint", nullable: false),
                    CurrentDetailsRevision = table.Column<long>(type: "bigint", nullable: false),
                    ChangedFieldsMask = table.Column<int>(type: "integer", nullable: false),
                    DetailsChangeEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_data_rights_correction_receipts", x => x.Id);
                    table.UniqueConstraint("AK_reservation_data_rights_correction_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_data_rights_correction_receipts_approval", "\"ApprovalRevision\" >= 1");
                    table.CheckConstraint("CK_reservation_data_rights_correction_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_reservation_data_rights_correction_receipts_fields", "\"ChangedFieldsMask\" BETWEEN 1 AND 127");
                    table.CheckConstraint("CK_reservation_data_rights_correction_receipts_versions", "\"SelectedRecordVersion\" >= 1 AND \"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1 AND \"SelectedDetailsRevision\" >= 0 AND \"CurrentDetailsRevision\" = \"SelectedDetailsRevision\" + 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_rights_correction_receipts_ScopeId_Details~",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "DetailsChangeEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_rights_correction_receipts_ScopeId_EventId",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_rights_correction_receipts_ScopeId_Idempot~",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_rights_correction_receipts_ScopeId_Propert~",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "PropertyId", "CaseId", "ApprovalRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_rights_correction_receipts_ScopeId_Reserva~",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "ReservationId", "CurrentRecordVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_data_rights_correction_receipts",
                schema: "reservations");
        }
    }
}
