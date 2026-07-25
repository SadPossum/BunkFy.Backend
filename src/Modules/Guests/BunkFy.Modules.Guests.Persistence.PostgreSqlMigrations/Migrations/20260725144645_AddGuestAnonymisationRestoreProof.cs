using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestAnonymisationRestoreProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_guest_anonymisation_tombstones_guest_anonymisation_receipts~",
                schema: "guests",
                table: "guest_anonymisation_tombstones");

            migrationBuilder.DropIndex(
                name: "IX_guest_anonymisation_tombstones_ScopeId_OwnerReceiptSha256",
                schema: "guests",
                table: "guest_anonymisation_tombstones");

            migrationBuilder.CreateTable(
                name: "guest_anonymisation_restore_receipts",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingGuestVersion = table.Column<long>(type: "bigint", nullable: false),
                    TombstoneRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_anonymisation_restore_receipts", x => x.Id);
                    table.UniqueConstraint("AK_guest_anonymisation_restore_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_anonymisation_restore_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_guest_anonymisation_restore_receipts_digests", "char_length(\"OwnerReceiptSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_guest_anonymisation_restore_receipts_identity", "\"LedgerEntryId\" = \"Id\"");
                    table.CheckConstraint("CK_guest_anonymisation_restore_receipts_versions", "\"OwnerReceiptContractVersion\" >= 1 AND \"ResultingGuestVersion\" >= 1 AND \"TombstoneRevision\" >= 1");
                    table.ForeignKey(
                        name: "FK_guest_anonymisation_restore_receipts_guest_anonymisation_to~",
                        columns: x => new { x.ScopeId, x.GuestId },
                        principalSchema: "guests",
                        principalTable: "guest_anonymisation_tombstones",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guest_anonymisation_restore_receipts_ScopeId_GuestId_Ledger~",
                schema: "guests",
                table: "guest_anonymisation_restore_receipts",
                columns: new[] { "ScopeId", "GuestId", "LedgerEntryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guest_anonymisation_restore_receipts",
                schema: "guests");

            migrationBuilder.CreateIndex(
                name: "IX_guest_anonymisation_tombstones_ScopeId_OwnerReceiptSha256",
                schema: "guests",
                table: "guest_anonymisation_tombstones",
                columns: new[] { "ScopeId", "OwnerReceiptSha256" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_guest_anonymisation_tombstones_guest_anonymisation_receipts~",
                schema: "guests",
                table: "guest_anonymisation_tombstones",
                columns: new[] { "ScopeId", "OwnerReceiptSha256" },
                principalSchema: "guests",
                principalTable: "guest_anonymisation_receipts",
                principalColumns: new[] { "ScopeId", "CanonicalSha256" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
