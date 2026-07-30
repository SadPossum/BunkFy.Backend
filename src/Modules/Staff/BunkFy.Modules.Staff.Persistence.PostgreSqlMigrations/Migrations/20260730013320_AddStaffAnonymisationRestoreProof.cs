using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAnonymisationRestoreProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_contract",
                schema: "staff",
                table: "staff_anonymisation_tombstones");

            migrationBuilder.Sql(
                """
                UPDATE staff.staff_anonymisation_tombstones
                SET "ContractVersion" = 2
                WHERE "ContractVersion" = 1;
                """);

            migrationBuilder.CreateTable(
                name: "staff_anonymisation_restore_receipts",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    TombstoneRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_anonymisation_restore_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_anonymisation_restore_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_anonymisation_restore_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_anonymisation_restore_receipts_digests", "char_length(\"OwnerReceiptSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_staff_anonymisation_restore_receipts_identity", "\"LedgerEntryId\" = \"Id\"");
                    table.CheckConstraint("CK_staff_anonymisation_restore_receipts_versions", "\"OwnerReceiptContractVersion\" >= 1 AND \"ResultingStaffVersion\" >= 1 AND \"TombstoneRevision\" >= 1");
                    table.ForeignKey(
                        name: "FK_staff_anonymisation_restore_receipts_staff_anonymisation_to~",
                        columns: x => new { x.ScopeId, x.StaffMemberId },
                        principalSchema: "staff",
                        principalTable: "staff_anonymisation_tombstones",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_contract",
                schema: "staff",
                table: "staff_anonymisation_tombstones",
                sql: "\"ContractVersion\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_staff_anonymisation_restore_receipts_ScopeId_StaffMemberId_~",
                schema: "staff",
                table: "staff_anonymisation_restore_receipts",
                columns: new[] { "ScopeId", "StaffMemberId", "LedgerEntryId" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER "TR_staff_anonymisation_restore_receipts_append_only"
                BEFORE UPDATE OR DELETE ON "staff"."staff_anonymisation_restore_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_receipt_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM staff.staff_anonymisation_restore_receipts
                    ) OR EXISTS (
                        SELECT 1
                        FROM staff.staff_anonymisation_tombstones
                        WHERE "LedgerEntryId" IS NOT NULL
                           OR "LastReplayedAtUtc" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade Staff anonymisation restore proof while restore evidence exists.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "staff_anonymisation_restore_receipts",
                schema: "staff");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_contract",
                schema: "staff",
                table: "staff_anonymisation_tombstones");

            migrationBuilder.Sql(
                """
                UPDATE staff.staff_anonymisation_tombstones
                SET "ContractVersion" = 1
                WHERE "ContractVersion" = 2;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_contract",
                schema: "staff",
                table: "staff_anonymisation_tombstones",
                sql: "\"ContractVersion\" = 1");
        }
    }
}
