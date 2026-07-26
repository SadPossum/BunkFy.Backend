using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationAnonymisationOwnerProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAtUtc",
                schema: "reservations",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymised",
                schema: "reservations",
                table: "reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "reservation_anonymisation_receipts",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    OperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    SelectedDetailsRevision = table.Column<long>(type: "bigint", nullable: false),
                    ResultingDetailsRevision = table.Column<long>(type: "bigint", nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    RedactedHistoryCount = table.Column<int>(type: "integer", nullable: false),
                    RemovedGuestLinkCount = table.Column<int>(type: "integer", nullable: false),
                    ReducedExternalOperationCount = table.Column<int>(type: "integer", nullable: false),
                    SuppressedReminderCount = table.Column<int>(type: "integer", nullable: false),
                    ApprovalEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_anonymisation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_reservation_anonymisation_receipts_ScopeId_CanonicalSha256", x => new { x.ScopeId, x.CanonicalSha256 });
                    table.UniqueConstraint("AK_reservation_anonymisation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_anonymisation_receipts_actor", "length(trim(\"ActorId\")) > 0");
                    table.CheckConstraint("CK_reservation_anonymisation_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_reservation_anonymisation_receipts_counts", "\"RedactedHistoryCount\" >= 1 AND \"RemovedGuestLinkCount\" >= 0 AND \"ReducedExternalOperationCount\" >= 0 AND \"SuppressedReminderCount\" >= 0");
                    table.CheckConstraint("CK_reservation_anonymisation_receipts_digests", "char_length(\"ApprovalEvidenceSha256\") = 64 AND char_length(\"PolicyEvidenceSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_reservation_anonymisation_receipts_outcome", "\"Disposition\" = 1 AND \"Reason\" = 1");
                    table.CheckConstraint("CK_reservation_anonymisation_receipts_revisions", "\"ApprovalRevision\" >= 1 AND \"OperationRevision\" > \"ApprovalRevision\"");
                    table.CheckConstraint("CK_reservation_anonymisation_receipts_versions", "\"SelectedReservationVersion\" >= 1 AND \"ResultingReservationVersion\" = \"SelectedReservationVersion\" + 1 AND \"SelectedDetailsRevision\" >= 1 AND \"ResultingDetailsRevision\" = \"SelectedDetailsRevision\" + 1");
                    table.ForeignKey(
                        name: "FK_reservation_anonymisation_receipts_reservations_ScopeId_Res~",
                        columns: x => new { x.ScopeId, x.ReservationId },
                        principalSchema: "reservations",
                        principalTable: "reservations",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_anonymisation_state",
                schema: "reservations",
                table: "reservations",
                sql: "(\"IsAnonymised\" = TRUE AND \"AnonymisedAtUtc\" IS NOT NULL) OR (\"IsAnonymised\" = FALSE AND \"AnonymisedAtUtc\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_anonymisation_receipts_ScopeId_CaseId_ApprovalR~",
                schema: "reservations",
                table: "reservation_anonymisation_receipts",
                columns: new[] { "ScopeId", "CaseId", "ApprovalRevision", "OperationRevision", "ReservationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_anonymisation_receipts_ScopeId_IdempotencyKey",
                schema: "reservations",
                table: "reservation_anonymisation_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_anonymisation_receipts_ScopeId_PropertyId_Reser~",
                schema: "reservations",
                table: "reservation_anonymisation_receipts",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "ResultingReservationVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_anonymisation_receipts_ScopeId_ReservationId",
                schema: "reservations",
                table: "reservation_anonymisation_receipts",
                columns: new[] { "ScopeId", "ReservationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_anonymisation_receipts",
                schema: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_anonymisation_state",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "IsAnonymised",
                schema: "reservations",
                table: "reservations");
        }
    }
}
