using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationAnonymisationRestoreProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservation_anonymisation_tombstones",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingDetailsRevision = table.Column<long>(type: "bigint", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_anonymisation_tombstones", x => x.Id);
                    table.UniqueConstraint("AK_reservation_anonymisation_tombstones_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_anonymisation_tombstones_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_reservation_anonymisation_tombstones_receipt", "\"OwnerReceiptContractVersion\" >= 1 AND char_length(\"OwnerReceiptSha256\") = 64");
                    table.CheckConstraint("CK_reservation_anonymisation_tombstones_replay", "(\"LedgerEntryId\" IS NULL AND \"LastReplayedAtUtc\" IS NULL) OR (\"LedgerEntryId\" IS NOT NULL AND \"LastReplayedAtUtc\" IS NOT NULL AND \"LastReplayedAtUtc\" >= \"CompletedAtUtc\")");
                    table.CheckConstraint("CK_reservation_anonymisation_tombstones_revision", "\"Revision\" >= 1");
                    table.CheckConstraint("CK_reservation_anonymisation_tombstones_versions", "\"ResultingReservationVersion\" >= 1 AND (\"ResultingDetailsRevision\" IS NULL OR \"ResultingDetailsRevision\" >= 1)");
                });

            migrationBuilder.Sql(
                """
                INSERT INTO reservations.reservation_anonymisation_tombstones
                    ("Id",
                     "ContractVersion",
                     "Revision",
                     "PropertyId",
                     "OwnerReceiptContractVersion",
                     "OwnerReceiptId",
                     "OwnerReceiptSha256",
                     "ResultingReservationVersion",
                     "ResultingDetailsRevision",
                     "CompletedAtUtc",
                     "LedgerEntryId",
                     "LastReplayedAtUtc",
                     "ScopeId")
                SELECT receipt."ReservationId",
                       1,
                       1,
                       receipt."PropertyId",
                       receipt."ContractVersion",
                       receipt."Id",
                       receipt."CanonicalSha256",
                       receipt."ResultingReservationVersion",
                       receipt."ResultingDetailsRevision",
                       receipt."CompletedAtUtc",
                       NULL,
                       NULL,
                       receipt."ScopeId"
                FROM reservations.reservation_anonymisation_receipts AS receipt;
                """);

            migrationBuilder.CreateTable(
                name: "reservation_anonymisation_restore_receipts",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantSequence = table.Column<long>(type: "bigint", nullable: false),
                    LedgerEntrySha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingDetailsRevision = table.Column<long>(type: "bigint", nullable: true),
                    OriginallyCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TombstoneRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_anonymisation_restore_receipts", x => x.Id);
                    table.UniqueConstraint("AK_reservation_anonymisation_restore_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_anonymisation_restore_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_reservation_anonymisation_restore_receipts_coordinates", "\"TenantSequence\" >= 1 AND \"OwnerReceiptContractVersion\" >= 1 AND \"ResultingReservationVersion\" >= 1 AND \"TombstoneRevision\" >= 1");
                    table.CheckConstraint("CK_reservation_anonymisation_restore_receipts_details", "\"ResultingDetailsRevision\" IS NULL OR \"ResultingDetailsRevision\" >= 1");
                    table.CheckConstraint("CK_reservation_anonymisation_restore_receipts_digests", "char_length(\"LedgerEntrySha256\") = 64 AND char_length(\"OwnerReceiptSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_reservation_anonymisation_restore_receipts_identity", "\"LedgerEntryId\" = \"Id\"");
                    table.CheckConstraint("CK_reservation_anonymisation_restore_receipts_times", "\"ReplayedAtUtc\" >= \"OriginallyCompletedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_reservation_anonymisation_restore_receipts_reservation_anon~",
                        columns: x => new { x.ScopeId, x.ReservationId },
                        principalSchema: "reservations",
                        principalTable: "reservation_anonymisation_tombstones",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_anonymisation_restore_receipts_ScopeId_Property~",
                schema: "reservations",
                table: "reservation_anonymisation_restore_receipts",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "LedgerEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_anonymisation_restore_receipts_ScopeId_Reservat~",
                schema: "reservations",
                table: "reservation_anonymisation_restore_receipts",
                columns: new[] { "ScopeId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_anonymisation_tombstones_ScopeId_LedgerEntryId",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones",
                columns: new[] { "ScopeId", "LedgerEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_anonymisation_tombstones_ScopeId_PropertyId_Com~",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones",
                columns: new[] { "ScopeId", "PropertyId", "CompletedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_anonymisation_restore_receipts",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservation_anonymisation_tombstones",
                schema: "reservations");
        }
    }
}
