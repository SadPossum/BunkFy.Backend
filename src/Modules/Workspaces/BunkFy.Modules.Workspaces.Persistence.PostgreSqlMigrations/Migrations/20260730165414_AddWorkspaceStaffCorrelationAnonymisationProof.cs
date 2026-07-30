using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceStaffCorrelationAnonymisationProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_correlation_anonymisation_receipts",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    OperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    AnchorProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    SelectedAnchorVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingAnchorVersion = table.Column<long>(type: "bigint", nullable: false),
                    OnboardingRecordsScrubbed = table.Column<int>(type: "integer", nullable: false),
                    AccessProcessRecordsScrubbed = table.Column<int>(type: "integer", nullable: false),
                    AccessPlanRecordsScrubbed = table.Column<int>(type: "integer", nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    ApprovalEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StateBindingSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingStateSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_correlation_anonymisation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_correlation_anonymisation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_correlation_anonymisation_receipt_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_receipt_counts", "\"OnboardingRecordsScrubbed\" >= 0 AND \"AccessProcessRecordsScrubbed\" > 0 AND \"AccessPlanRecordsScrubbed\" >= 0");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_receipt_hashes", "char_length(\"ApprovalEvidenceSha256\") = 64 AND char_length(\"StateBindingSha256\") = 64 AND char_length(\"ResultingStateSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_receipt_outcome", "\"Disposition\" = 1 AND \"Reason\" = 1");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_receipt_revisions", "\"ApprovalRevision\" > 0 AND \"OperationRevision\" > \"ApprovalRevision\"");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_receipt_versions", "\"SelectedStaffVersion\" > 0 AND \"SelectedAnchorVersion\" > 0 AND \"ResultingAnchorVersion\" = \"SelectedAnchorVersion\" + 1");
                });

            migrationBuilder.CreateTable(
                name: "staff_correlation_anonymisation_tombstones",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    SelectedAnchorVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingAnchorVersion = table.Column<long>(type: "bigint", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingStateSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_correlation_anonymisation_tombstones", x => x.Id);
                    table.UniqueConstraint("AK_staff_correlation_anonymisation_tombstones_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_correlation_anonymisation_tombstone_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_tombstone_hashes", "char_length(\"OwnerReceiptSha256\") = 64 AND char_length(\"ResultingStateSha256\") = 64");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_tombstone_receipt", "\"OwnerReceiptContractVersion\" > 0");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_tombstone_replay", "(\"LedgerEntryId\" IS NULL AND \"LastReplayedAtUtc\" IS NULL) OR (\"LedgerEntryId\" IS NOT NULL AND \"LastReplayedAtUtc\" IS NOT NULL AND \"LastReplayedAtUtc\" >= \"CompletedAtUtc\")");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_tombstone_revision", "\"Revision\" > 0");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_tombstone_versions", "\"SelectedStaffVersion\" > 0 AND \"SelectedAnchorVersion\" > 0 AND \"ResultingAnchorVersion\" = \"SelectedAnchorVersion\" + 1");
                });

            migrationBuilder.CreateTable(
                name: "staff_correlation_anonymisation_restore_receipts",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantSequence = table.Column<long>(type: "bigint", nullable: false),
                    LedgerEntrySha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    AnchorProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingAnchorVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingStateSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OnboardingRecordsScrubbed = table.Column<int>(type: "integer", nullable: false),
                    AccessProcessRecordsScrubbed = table.Column<int>(type: "integer", nullable: false),
                    AccessPlanRecordsScrubbed = table.Column<int>(type: "integer", nullable: false),
                    OriginallyCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TombstoneRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_correlation_anonymisation_restore_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_correlation_anonymisation_restore_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_correlation_anonymisation_restore_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_restore_counts", "\"OnboardingRecordsScrubbed\" >= 0 AND \"AccessProcessRecordsScrubbed\" > 0 AND \"AccessPlanRecordsScrubbed\" >= 0");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_restore_hashes", "char_length(\"LedgerEntrySha256\") = 64 AND char_length(\"OwnerReceiptSha256\") = 64 AND char_length(\"ResultingStateSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_restore_identity", "\"LedgerEntryId\" = \"Id\"");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_restore_receipt", "\"TenantSequence\" > 0 AND \"OwnerReceiptContractVersion\" > 0");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_restore_revision", "\"TombstoneRevision\" > 0");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_restore_times", "\"ReplayedAtUtc\" >= \"OriginallyCompletedAtUtc\"");
                    table.CheckConstraint("CK_staff_correlation_anonymisation_restore_version", "\"ResultingAnchorVersion\" > 1");
                    table.ForeignKey(
                        name: "FK_staff_correlation_anonymisation_restore_receipts_staff_corr~",
                        columns: x => new { x.ScopeId, x.AnchorProcessId },
                        principalSchema: "workspaces",
                        principalTable: "staff_correlation_anonymisation_tombstones",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_correlation_anonymisation_receipts_ScopeId_AnchorProc~",
                schema: "workspaces",
                table: "staff_correlation_anonymisation_receipts",
                columns: new[] { "ScopeId", "AnchorProcessId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_correlation_anonymisation_receipts_ScopeId_Idempotenc~",
                schema: "workspaces",
                table: "staff_correlation_anonymisation_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_correlation_anonymisation_receipts_ScopeId_StaffMembe~",
                schema: "workspaces",
                table: "staff_correlation_anonymisation_receipts",
                columns: new[] { "ScopeId", "StaffMemberId", "SelectedStaffVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_correlation_anonymisation_restore_receipts_ScopeId_An~",
                schema: "workspaces",
                table: "staff_correlation_anonymisation_restore_receipts",
                columns: new[] { "ScopeId", "AnchorProcessId", "LedgerEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_correlation_anonymisation_tombstones_ScopeId_LedgerEn~",
                schema: "workspaces",
                table: "staff_correlation_anonymisation_tombstones",
                columns: new[] { "ScopeId", "LedgerEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_correlation_anonymisation_tombstones_ScopeId_OwnerRec~",
                schema: "workspaces",
                table: "staff_correlation_anonymisation_tombstones",
                columns: new[] { "ScopeId", "OwnerReceiptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_correlation_anonymisation_tombstones_ScopeId_StaffMem~",
                schema: "workspaces",
                table: "staff_correlation_anonymisation_tombstones",
                columns: new[] { "ScopeId", "StaffMemberId", "SelectedStaffVersion" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER
                    "TR_staff_correlation_anonymisation_receipts_append_only"
                BEFORE UPDATE OR DELETE
                ON
                    workspaces.staff_correlation_anonymisation_receipts
                FOR EACH ROW
                EXECUTE FUNCTION workspaces.prevent_receipt_mutation();

                CREATE TRIGGER
                    "TR_staff_correlation_anonymisation_restore_receipts_append_only"
                BEFORE UPDATE OR DELETE
                ON
                    workspaces.staff_correlation_anonymisation_restore_receipts
                FOR EACH ROW
                EXECUTE FUNCTION workspaces.prevent_receipt_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_staff_correlation_anonymisation_receipts_append_only"
                    ON
                    workspaces.staff_correlation_anonymisation_receipts;

                DROP TRIGGER IF EXISTS
                    "TR_staff_correlation_anonymisation_restore_receipts_append_only"
                    ON
                    workspaces.staff_correlation_anonymisation_restore_receipts;
                """);

            migrationBuilder.DropTable(
                name: "staff_correlation_anonymisation_receipts",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "staff_correlation_anonymisation_restore_receipts",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "staff_correlation_anonymisation_tombstones",
                schema: "workspaces");
        }
    }
}
