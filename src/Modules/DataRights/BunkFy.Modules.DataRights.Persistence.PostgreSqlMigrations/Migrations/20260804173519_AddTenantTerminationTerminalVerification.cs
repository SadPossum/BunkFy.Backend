using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTerminationTerminalVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_export_confirmation",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_operation",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_outcome",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.AddColumn<long>(
                name: "DestroyCompletedOperationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DestroyedAtUtc",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FreezeOperationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FrozenAtUtc",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrozenBy",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrozenRevisionSha256",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerminalReceiptId",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TerminalReceiptVersion",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VerificationConfirmationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationConfirmedAtUtc",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationConfirmedBy",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VerificationConfirmedOperationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationOwnerProofSetSha256",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WorkspaceFenceRevision",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_termination_frozen_export_owners",
                schema: "data-rights",
                columns: table => new
                {
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    CatalogVersion = table.Column<int>(type: "integer", nullable: false),
                    CatalogSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_termination_frozen_export_owners", x => new { x.ProcessId, x.Ordinal });
                    table.CheckConstraint("CK_data_rights_tenant_termination_frozen_owner_catalog", "\"ContractVersion\" >= 1 AND \"CatalogVersion\" >= 1 AND char_length(\"CatalogSha256\") = 64 AND \"CatalogSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_data_rights_tenant_termination_frozen_owner_key", "length(trim(\"OwnerKey\")) > 0 AND \"OwnerKey\" ~ '^[a-z0-9][a-z0-9._-]{0,99}$'");
                    table.CheckConstraint("CK_data_rights_tenant_termination_frozen_owner_ordinal", "\"Ordinal\" BETWEEN 1 AND 64");
                    table.ForeignKey(
                        name: "FK_tenant_termination_frozen_export_owners_tenant_termination_~",
                        column: x => x.ProcessId,
                        principalSchema: "data-rights",
                        principalTable: "tenant_termination_processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_termination_terminal_receipts",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    TerminationEpoch = table.Column<Guid>(type: "uuid", nullable: false),
                    DestroyOperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    VerificationOperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    VerificationTaskRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationTaskAttempt = table.Column<int>(type: "integer", nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    FrozenRevisionSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExportRequested = table.Column<bool>(type: "boolean", nullable: false),
                    ExportArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExportArtifactVersion = table.Column<long>(type: "bigint", nullable: true),
                    ExportFragmentSetSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    OwnerCount = table.Column<int>(type: "integer", nullable: false),
                    OwnerProofSetSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    TerminalOwnerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TerminalOwnerSelectedProofRevision = table.Column<long>(type: "bigint", nullable: false),
                    TerminalOwnerResultingProofRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReplayCheckpointSequence = table.Column<long>(type: "bigint", nullable: false),
                    ReplayCheckpointRecordSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReplayIntegrityKeyVersion = table.Column<int>(type: "integer", nullable: false),
                    ReplayCheckpointProofSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReplayCheckpointFlushedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SealedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SealedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_termination_terminal_receipts", x => x.Id);
                    table.UniqueConstraint("AK_tenant_termination_terminal_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.UniqueConstraint("AK_tenant_termination_terminal_receipts_ScopeId_ProcessId_Id_V~", x => new { x.ScopeId, x.ProcessId, x.Id, x.Version });
                    table.CheckConstraint("CK_data_rights_tenant_termination_terminal_receipt_coordinates", "\"ApprovalRevision\" >= 1 AND \"DestroyOperationRevision\" > \"ApprovalRevision\" AND \"VerificationOperationRevision\" > \"DestroyOperationRevision\" AND \"VerificationTaskRunId\" IS NOT NULL AND \"VerificationTaskAttempt\" > 0 AND char_length(\"PolicyEvidenceSha256\") = 64 AND \"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$' AND char_length(\"FrozenRevisionSha256\") = 64 AND \"FrozenRevisionSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_data_rights_tenant_termination_terminal_receipt_export", "(\"ExportRequested\" = FALSE AND \"ExportArtifactId\" IS NULL AND \"ExportArtifactVersion\" IS NULL AND \"ExportFragmentSetSha256\" IS NULL) OR (\"ExportRequested\" = TRUE AND \"ExportArtifactId\" IS NOT NULL AND \"ExportArtifactVersion\" >= 1 AND char_length(\"ExportFragmentSetSha256\") = 64 AND \"ExportFragmentSetSha256\" ~ '^[0-9a-f]{64}$')");
                    table.CheckConstraint("CK_data_rights_tenant_termination_terminal_receipt_owners", "\"OwnerCount\" BETWEEN 1 AND 64 AND char_length(\"OwnerProofSetSha256\") = 64 AND \"OwnerProofSetSha256\" ~ '^[0-9a-f]{64}$' AND length(trim(\"TerminalOwnerKey\")) > 0 AND \"TerminalOwnerKey\" ~ '^[a-z0-9][a-z0-9._-]{0,99}$' AND \"TerminalOwnerSelectedProofRevision\" >= 0 AND \"TerminalOwnerResultingProofRevision\" >= \"TerminalOwnerSelectedProofRevision\"");
                    table.CheckConstraint("CK_data_rights_tenant_termination_terminal_receipt_replay", "\"ReplayCheckpointSequence\" > 0 AND char_length(\"ReplayCheckpointRecordSha256\") = 64 AND \"ReplayCheckpointRecordSha256\" ~ '^[0-9a-f]{64}$' AND \"ReplayIntegrityKeyVersion\" > 0 AND char_length(\"ReplayCheckpointProofSha256\") = 64 AND \"ReplayCheckpointProofSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_data_rights_tenant_termination_terminal_receipt_seal", "length(trim(\"SealedBy\")) > 0 AND \"SealedAtUtc\" >= \"ReplayCheckpointFlushedAtUtc\"");
                    table.CheckConstraint("CK_data_rights_tenant_termination_terminal_receipt_version", "\"Version\" = 1");
                    table.ForeignKey(
                        name: "FK_tenant_termination_terminal_receipts_tenant_termination_pro~",
                        columns: x => new { x.ScopeId, x.ProcessId, x.CaseId, x.ApprovalRevision, x.TerminationEpoch, x.PolicyEvidenceSha256 },
                        principalSchema: "data-rights",
                        principalTable: "tenant_termination_processes",
                        principalColumns: new[] { "ScopeId", "Id", "CaseId", "ApprovalRevision", "TerminationEpoch", "PolicyEvidenceSha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_processes_ScopeId_Id_TerminalReceiptId_T~",
                schema: "data-rights",
                table: "tenant_termination_processes",
                columns: new[] { "ScopeId", "Id", "TerminalReceiptId", "TerminalReceiptVersion" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_destroy_checkpoint",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "((\"Phase\" IN (1, 2, 3, 6) AND \"DestroyCompletedOperationRevision\" IS NULL AND \"DestroyedAtUtc\" IS NULL) OR (\"Phase\" IN (4, 5) AND \"DestroyCompletedOperationRevision\" > \"ApprovalRevision\" AND \"DestroyCompletedOperationRevision\" <= \"OperationRevision\" AND \"DestroyedAtUtc\" >= \"CreatedAtUtc\" AND \"DestroyedAtUtc\" <= \"LastChangedAtUtc\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_export_confirmation",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"ExportConfirmationRevision\" >= 0 AND ((\"ExportConfirmedOperationRevision\" IS NULL AND \"ExportArtifactId\" IS NULL AND \"ExportArtifactVersion\" IS NULL AND \"ExportFrozenRevisionSha256\" IS NULL AND \"ExportFragmentSetSha256\" IS NULL AND \"ExportConfirmedBy\" IS NULL AND \"ExportConfirmedAtUtc\" IS NULL) OR (\"ExportConfirmationRevision\" >= 1 AND \"ExportConfirmedOperationRevision\" >= 1 AND \"ExportConfirmedOperationRevision\" <= \"OperationRevision\" AND \"ExportArtifactId\" IS NOT NULL AND \"ExportArtifactVersion\" >= 1 AND char_length(\"ExportFrozenRevisionSha256\") = 64 AND \"ExportFrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND \"ExportFrozenRevisionSha256\" = \"FrozenRevisionSha256\" AND char_length(\"ExportFragmentSetSha256\") = 64 AND \"ExportFragmentSetSha256\" ~ '^[0-9a-f]{64}$' AND length(trim(\"ExportConfirmedBy\")) > 0 AND \"ExportConfirmedAtUtc\" >= \"CreatedAtUtc\" AND \"ExportConfirmedAtUtc\" <= \"LastChangedAtUtc\")) AND ((\"ExportRequested\" = FALSE AND \"ExportConfirmationRevision\" = 0 AND \"ExportConfirmedOperationRevision\" IS NULL) OR \"ExportRequested\" = TRUE) AND (\"ExportRequested\" = FALSE OR \"Phase\" IN (1, 2, 6) OR \"ExportConfirmedOperationRevision\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_freeze_checkpoint",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "((\"Phase\" = 1 AND \"FreezeOperationRevision\" IS NULL AND \"WorkspaceFenceRevision\" IS NULL AND \"FrozenRevisionSha256\" IS NULL AND \"FrozenBy\" IS NULL AND \"FrozenAtUtc\" IS NULL) OR (\"Phase\" BETWEEN 2 AND 6 AND \"FreezeOperationRevision\" > \"ApprovalRevision\" AND \"FreezeOperationRevision\" <= \"OperationRevision\" AND \"WorkspaceFenceRevision\" >= 1 AND char_length(\"FrozenRevisionSha256\") = 64 AND \"FrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND length(trim(\"FrozenBy\")) > 0 AND \"FrozenAtUtc\" >= \"CreatedAtUtc\" AND \"FrozenAtUtc\" <= \"LastChangedAtUtc\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_operation",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"OperationRevision\" >= \"ApprovalRevision\" AND ((\"Status\" = 1 AND \"OperationRevision\" >= \"ApprovalRevision\") OR (\"Status\" BETWEEN 2 AND 6 AND \"OperationRevision\" > \"ApprovalRevision\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_outcome",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "(\"Status\" = 3 AND \"OutcomeCode\" IS NOT NULL AND (\"HoldReviewAtUtc\" IS NULL OR \"HoldReviewAtUtc\" >= \"LastChangedAtUtc\")) OR (\"Status\" = 4 AND \"OutcomeCode\" IS NOT NULL AND \"HoldReviewAtUtc\" IS NULL) OR (\"Status\" IN (1, 2, 5, 6) AND \"OutcomeCode\" IS NULL AND \"HoldReviewAtUtc\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_verification_confirm~",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"VerificationConfirmationRevision\" >= 0 AND ((\"VerificationConfirmedOperationRevision\" IS NULL AND \"TerminalReceiptId\" IS NULL AND \"TerminalReceiptVersion\" IS NULL AND \"VerificationOwnerProofSetSha256\" IS NULL AND \"VerificationConfirmedBy\" IS NULL AND \"VerificationConfirmedAtUtc\" IS NULL) OR (\"Phase\" IN (4, 5) AND \"VerificationConfirmationRevision\" >= 1 AND \"VerificationConfirmedOperationRevision\" = \"OperationRevision\" AND \"VerificationConfirmedOperationRevision\" > \"DestroyCompletedOperationRevision\" AND \"TerminalReceiptId\" IS NOT NULL AND \"TerminalReceiptVersion\" >= 1 AND char_length(\"VerificationOwnerProofSetSha256\") = 64 AND \"VerificationOwnerProofSetSha256\" ~ '^[0-9a-f]{64}$' AND length(trim(\"VerificationConfirmedBy\")) > 0 AND \"VerificationConfirmedAtUtc\" >= \"DestroyedAtUtc\" AND \"VerificationConfirmedAtUtc\" <= \"LastChangedAtUtc\")) AND (\"Phase\" <> 5 OR \"VerificationConfirmedOperationRevision\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_frozen_export_owners_ProcessId_OwnerKey",
                schema: "data-rights",
                table: "tenant_termination_frozen_export_owners",
                columns: new[] { "ProcessId", "OwnerKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_terminal_receipts_ScopeId_IdempotencyKey",
                schema: "data-rights",
                table: "tenant_termination_terminal_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_terminal_receipts_ScopeId_ProcessId_Case~",
                schema: "data-rights",
                table: "tenant_termination_terminal_receipts",
                columns: new[] { "ScopeId", "ProcessId", "CaseId", "ApprovalRevision", "TerminationEpoch", "PolicyEvidenceSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_terminal_receipts_ScopeId_ProcessId_Veri~",
                schema: "data-rights",
                table: "tenant_termination_terminal_receipts",
                columns: new[] { "ScopeId", "ProcessId", "VerificationOperationRevision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_termination_processes_tenant_termination_terminal_re~",
                schema: "data-rights",
                table: "tenant_termination_processes",
                columns: new[] { "ScopeId", "Id", "TerminalReceiptId", "TerminalReceiptVersion" },
                principalSchema: "data-rights",
                principalTable: "tenant_termination_terminal_receipts",
                principalColumns: new[] { "ScopeId", "ProcessId", "Id", "Version" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_termination_processes_tenant_termination_terminal_re~",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropTable(
                name: "tenant_termination_frozen_export_owners",
                schema: "data-rights");

            migrationBuilder.DropTable(
                name: "tenant_termination_terminal_receipts",
                schema: "data-rights");

            migrationBuilder.DropIndex(
                name: "IX_tenant_termination_processes_ScopeId_Id_TerminalReceiptId_T~",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_destroy_checkpoint",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_export_confirmation",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_freeze_checkpoint",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_operation",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_outcome",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_verification_confirm~",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "DestroyCompletedOperationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "DestroyedAtUtc",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "FreezeOperationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "FrozenAtUtc",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "FrozenBy",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "FrozenRevisionSha256",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "TerminalReceiptId",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "TerminalReceiptVersion",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "VerificationConfirmationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "VerificationConfirmedAtUtc",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "VerificationConfirmedBy",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "VerificationConfirmedOperationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "VerificationOwnerProofSetSha256",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "WorkspaceFenceRevision",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_export_confirmation",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"ExportConfirmationRevision\" >= 0 AND ((\"ExportConfirmedOperationRevision\" IS NULL AND \"ExportArtifactId\" IS NULL AND \"ExportArtifactVersion\" IS NULL AND \"ExportFrozenRevisionSha256\" IS NULL AND \"ExportFragmentSetSha256\" IS NULL AND \"ExportConfirmedBy\" IS NULL AND \"ExportConfirmedAtUtc\" IS NULL) OR (\"ExportConfirmationRevision\" >= 1 AND \"ExportConfirmedOperationRevision\" >= 1 AND \"ExportConfirmedOperationRevision\" <= \"OperationRevision\" AND \"ExportArtifactId\" IS NOT NULL AND \"ExportArtifactVersion\" >= 1 AND char_length(\"ExportFrozenRevisionSha256\") = 64 AND \"ExportFrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND char_length(\"ExportFragmentSetSha256\") = 64 AND \"ExportFragmentSetSha256\" ~ '^[0-9a-f]{64}$' AND length(trim(\"ExportConfirmedBy\")) > 0 AND \"ExportConfirmedAtUtc\" >= \"CreatedAtUtc\" AND \"ExportConfirmedAtUtc\" <= \"LastChangedAtUtc\")) AND ((\"ExportRequested\" = FALSE AND \"ExportConfirmationRevision\" = 0 AND \"ExportConfirmedOperationRevision\" IS NULL) OR \"ExportRequested\" = TRUE) AND (\"ExportRequested\" = FALSE OR \"Phase\" IN (1, 2, 6) OR \"ExportConfirmedOperationRevision\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_operation",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"OperationRevision\" >= 0 AND ((\"Status\" = 1 AND \"OperationRevision\" >= 0) OR (\"Status\" BETWEEN 2 AND 6 AND \"OperationRevision\" >= 1))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_outcome",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "(\"Status\" = 3 AND \"OutcomeCode\" IS NOT NULL AND \"HoldReviewAtUtc\" IS NOT NULL AND \"HoldReviewAtUtc\" >= \"LastChangedAtUtc\") OR (\"Status\" = 4 AND \"OutcomeCode\" IS NOT NULL AND \"HoldReviewAtUtc\" IS NULL) OR (\"Status\" IN (1, 2, 5, 6) AND \"OutcomeCode\" IS NULL AND \"HoldReviewAtUtc\" IS NULL)");
        }
    }
}
