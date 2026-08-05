using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTerminationExportArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExportArtifactId",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExportArtifactVersion",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExportConfirmationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExportConfirmedAtUtc",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportConfirmedBy",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExportConfirmedOperationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportFragmentSetSha256",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportFrozenRevisionSha256",
                schema: "data-rights",
                table: "tenant_termination_processes",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_termination_export_artifacts",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    FreezeOperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    ExportOperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    TerminationEpoch = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    FrozenRevisionSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ExpectedFragmentCount = table.Column<int>(type: "integer", nullable: false),
                    FragmentSetSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GenerationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    GenerationAttempt = table.Column<int>(type: "integer", nullable: true),
                    GenerationStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FragmentCount = table.Column<int>(type: "integer", nullable: true),
                    RecordCount = table.Column<long>(type: "bigint", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EncryptedByteLength = table.Column<long>(type: "bigint", nullable: true),
                    PlaintextSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    EncryptionKeyVersion = table.Column<int>(type: "integer", nullable: true),
                    FormatVersion = table.Column<int>(type: "integer", nullable: true),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletionRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_termination_export_artifacts", x => x.Id);
                    table.UniqueConstraint("AK_tenant_termination_export_artifacts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_artifact_coordinates", "\"ApprovalRevision\" >= 1 AND \"FreezeOperationRevision\" >= 1 AND \"ExportOperationRevision\" > \"FreezeOperationRevision\" AND \"ExpectedFragmentCount\" BETWEEN 1 AND 100 AND char_length(\"FrozenRevisionSha256\") = 64 AND \"FrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND char_length(\"PolicyEvidenceSha256\") = 64 AND \"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$' AND char_length(\"FragmentSetSha256\") = 64 AND \"FragmentSetSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_artifact_failure", "(\"State\" = 4 AND \"FailureCode\" IS NOT NULL) OR (\"State\" <> 4 AND \"FailureCode\" IS NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_artifact_fragments", "(\"FragmentCount\" IS NULL AND \"RecordCount\" IS NULL) OR (\"FragmentCount\" = \"ExpectedFragmentCount\" AND \"RecordCount\" BETWEEN 0 AND 100000000)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_artifact_generation", "(\"GenerationRunId\" IS NULL AND \"GenerationAttempt\" IS NULL AND \"GenerationStartedAtUtc\" IS NULL) OR (\"GenerationRunId\" IS NOT NULL AND \"GenerationAttempt\" > 0 AND \"GenerationStartedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_artifact_lifecycle", "(\"State\" = 1 AND \"GenerationRunId\" IS NULL AND \"FailureCode\" IS NULL AND \"FragmentCount\" IS NULL AND \"StorageKey\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 2 AND \"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NULL AND \"FragmentCount\" IS NULL AND \"StorageKey\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 3 AND \"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NULL AND \"FragmentCount\" IS NOT NULL AND \"StorageKey\" IS NOT NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 4 AND \"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NOT NULL AND \"FragmentCount\" IS NULL AND \"StorageKey\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 5 AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 6 AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND \"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 7 AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND \"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_artifact_state", "\"State\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_artifact_storage", "(\"StorageKey\" IS NULL AND \"EncryptedByteLength\" IS NULL AND \"PlaintextSha256\" IS NULL AND \"EncryptionKeyVersion\" IS NULL AND \"FormatVersion\" IS NULL AND \"AvailableAtUtc\" IS NULL) OR (\"StorageKey\" IS NOT NULL AND \"EncryptedByteLength\" > 0 AND char_length(\"PlaintextSha256\") = 64 AND \"PlaintextSha256\" ~ '^[0-9a-f]{64}$' AND \"EncryptionKeyVersion\" > 0 AND \"FormatVersion\" > 0 AND \"AvailableAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_artifact_timestamps", "\"ExpiresAtUtc\" > \"RequestedAtUtc\" AND (\"GenerationStartedAtUtc\" IS NULL OR (\"GenerationStartedAtUtc\" >= \"RequestedAtUtc\" AND \"GenerationStartedAtUtc\" < \"ExpiresAtUtc\")) AND (\"AvailableAtUtc\" IS NULL OR (\"GenerationStartedAtUtc\" IS NOT NULL AND \"AvailableAtUtc\" >= \"GenerationStartedAtUtc\" AND \"AvailableAtUtc\" < \"ExpiresAtUtc\")) AND (\"DeletionStartedAtUtc\" IS NULL OR \"DeletionStartedAtUtc\" >= \"ExpiresAtUtc\") AND (\"DeletedAtUtc\" IS NULL OR (\"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" >= \"DeletionStartedAtUtc\"))");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_artifact_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_tenant_termination_export_artifacts_tenant_termination_proc~",
                        columns: x => new { x.ScopeId, x.ProcessId, x.CaseId, x.ApprovalRevision, x.TerminationEpoch, x.PolicyEvidenceSha256 },
                        principalSchema: "data-rights",
                        principalTable: "tenant_termination_processes",
                        principalColumns: new[] { "ScopeId", "Id", "CaseId", "ApprovalRevision", "TerminationEpoch", "PolicyEvidenceSha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_termination_export_fragments",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    FreezeOperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    ExportOperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    TerminationEpoch = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerContractVersion = table.Column<int>(type: "integer", nullable: false),
                    CatalogVersion = table.Column<int>(type: "integer", nullable: false),
                    CatalogSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    FrozenRevisionSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GenerationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    GenerationAttempt = table.Column<int>(type: "integer", nullable: true),
                    GenerationStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecordCount = table.Column<long>(type: "bigint", nullable: true),
                    SelectedProofRevision = table.Column<long>(type: "bigint", nullable: true),
                    ResultingProofRevision = table.Column<long>(type: "bigint", nullable: true),
                    ResultCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EncryptedByteLength = table.Column<long>(type: "bigint", nullable: true),
                    PlaintextSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    EncryptionKeyVersion = table.Column<int>(type: "integer", nullable: true),
                    FormatVersion = table.Column<int>(type: "integer", nullable: true),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletionRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_termination_export_fragments", x => x.Id);
                    table.UniqueConstraint("AK_tenant_termination_export_fragments_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_fragment_coordinates", "\"ApprovalRevision\" >= 1 AND \"FreezeOperationRevision\" >= 1 AND \"ExportOperationRevision\" > \"FreezeOperationRevision\" AND \"OwnerContractVersion\" >= 1 AND \"CatalogVersion\" >= 1 AND length(trim(\"OwnerKey\")) > 0 AND \"OwnerKey\" ~ '^[a-z0-9][a-z0-9._-]{0,99}$' AND char_length(\"CatalogSha256\") = 64 AND \"CatalogSha256\" ~ '^[0-9a-f]{64}$' AND char_length(\"FrozenRevisionSha256\") = 64 AND \"FrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND char_length(\"PolicyEvidenceSha256\") = 64 AND \"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_fragment_failure", "(\"State\" = 4 AND \"FailureCode\" IS NOT NULL) OR (\"State\" <> 4 AND \"FailureCode\" IS NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_fragment_generation", "(\"GenerationRunId\" IS NULL AND \"GenerationAttempt\" IS NULL AND \"GenerationStartedAtUtc\" IS NULL) OR (\"GenerationRunId\" IS NOT NULL AND \"GenerationAttempt\" > 0 AND \"GenerationStartedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_fragment_lifecycle", "(\"State\" = 1 AND \"GenerationRunId\" IS NULL AND \"FailureCode\" IS NULL AND \"RecordCount\" IS NULL AND \"StorageKey\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 2 AND \"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NULL AND \"RecordCount\" IS NULL AND \"StorageKey\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 3 AND \"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NULL AND \"RecordCount\" IS NOT NULL AND \"StorageKey\" IS NOT NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 4 AND \"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NOT NULL AND \"RecordCount\" IS NULL AND \"StorageKey\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 5 AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 6 AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND \"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 7 AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND \"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_fragment_proof", "(\"RecordCount\" IS NULL AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultCode\" IS NULL) OR (\"RecordCount\" BETWEEN 0 AND 1000000 AND \"SelectedProofRevision\" >= 1 AND \"ResultingProofRevision\" = \"SelectedProofRevision\" AND \"ResultCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_fragment_state", "\"State\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_fragment_storage", "(\"StorageKey\" IS NULL AND \"EncryptedByteLength\" IS NULL AND \"PlaintextSha256\" IS NULL AND \"EncryptionKeyVersion\" IS NULL AND \"FormatVersion\" IS NULL AND \"AvailableAtUtc\" IS NULL) OR (\"StorageKey\" IS NOT NULL AND \"EncryptedByteLength\" > 0 AND char_length(\"PlaintextSha256\") = 64 AND \"PlaintextSha256\" ~ '^[0-9a-f]{64}$' AND \"EncryptionKeyVersion\" > 0 AND \"FormatVersion\" > 0 AND \"AvailableAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_fragment_timestamps", "\"ExpiresAtUtc\" > \"RequestedAtUtc\" AND (\"GenerationStartedAtUtc\" IS NULL OR (\"GenerationStartedAtUtc\" >= \"RequestedAtUtc\" AND \"GenerationStartedAtUtc\" < \"ExpiresAtUtc\")) AND (\"AvailableAtUtc\" IS NULL OR (\"GenerationStartedAtUtc\" IS NOT NULL AND \"AvailableAtUtc\" >= \"GenerationStartedAtUtc\" AND \"AvailableAtUtc\" < \"ExpiresAtUtc\")) AND (\"DeletionStartedAtUtc\" IS NULL OR \"DeletionStartedAtUtc\" >= \"ExpiresAtUtc\") AND (\"DeletedAtUtc\" IS NULL OR (\"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" >= \"DeletionStartedAtUtc\"))");
                    table.CheckConstraint("CK_data_rights_tenant_termination_export_fragment_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_tenant_termination_export_fragments_tenant_termination_owne~",
                        columns: x => new { x.ScopeId, x.Id },
                        principalSchema: "data-rights",
                        principalTable: "tenant_termination_owner_work_items",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_export_confirmation",
                schema: "data-rights",
                table: "tenant_termination_processes",
                sql: "\"ExportConfirmationRevision\" >= 0 AND ((\"ExportConfirmedOperationRevision\" IS NULL AND \"ExportArtifactId\" IS NULL AND \"ExportArtifactVersion\" IS NULL AND \"ExportFrozenRevisionSha256\" IS NULL AND \"ExportFragmentSetSha256\" IS NULL AND \"ExportConfirmedBy\" IS NULL AND \"ExportConfirmedAtUtc\" IS NULL) OR (\"ExportConfirmationRevision\" >= 1 AND \"ExportConfirmedOperationRevision\" >= 1 AND \"ExportConfirmedOperationRevision\" <= \"OperationRevision\" AND \"ExportArtifactId\" IS NOT NULL AND \"ExportArtifactVersion\" >= 1 AND char_length(\"ExportFrozenRevisionSha256\") = 64 AND \"ExportFrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND char_length(\"ExportFragmentSetSha256\") = 64 AND \"ExportFragmentSetSha256\" ~ '^[0-9a-f]{64}$' AND length(trim(\"ExportConfirmedBy\")) > 0 AND \"ExportConfirmedAtUtc\" >= \"CreatedAtUtc\" AND \"ExportConfirmedAtUtc\" <= \"LastChangedAtUtc\")) AND ((\"ExportRequested\" = FALSE AND \"ExportConfirmationRevision\" = 0 AND \"ExportConfirmedOperationRevision\" IS NULL) OR \"ExportRequested\" = TRUE) AND (\"ExportRequested\" = FALSE OR \"Phase\" IN (1, 2, 6) OR \"ExportConfirmedOperationRevision\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_export_artifacts_ScopeId_IdempotencyKey",
                schema: "data-rights",
                table: "tenant_termination_export_artifacts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_export_artifacts_ScopeId_ProcessId_CaseI~",
                schema: "data-rights",
                table: "tenant_termination_export_artifacts",
                columns: new[] { "ScopeId", "ProcessId", "CaseId", "ApprovalRevision", "TerminationEpoch", "PolicyEvidenceSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_export_artifacts_ScopeId_ProcessId_Expor~",
                schema: "data-rights",
                table: "tenant_termination_export_artifacts",
                columns: new[] { "ScopeId", "ProcessId", "ExportOperationRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_export_artifacts_ScopeId_State_ExpiresAt~",
                schema: "data-rights",
                table: "tenant_termination_export_artifacts",
                columns: new[] { "ScopeId", "State", "ExpiresAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_export_fragments_ScopeId_IdempotencyKey",
                schema: "data-rights",
                table: "tenant_termination_export_fragments",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_export_fragments_ScopeId_ProcessId_Expo~1",
                schema: "data-rights",
                table: "tenant_termination_export_fragments",
                columns: new[] { "ScopeId", "ProcessId", "ExportOperationRevision", "State", "OwnerKey", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_export_fragments_ScopeId_ProcessId_Expor~",
                schema: "data-rights",
                table: "tenant_termination_export_fragments",
                columns: new[] { "ScopeId", "ProcessId", "ExportOperationRevision", "OwnerKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_export_fragments_ScopeId_State_ExpiresAt~",
                schema: "data-rights",
                table: "tenant_termination_export_fragments",
                columns: new[] { "ScopeId", "State", "ExpiresAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_termination_export_artifacts",
                schema: "data-rights");

            migrationBuilder.DropTable(
                name: "tenant_termination_export_fragments",
                schema: "data-rights");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_process_export_confirmation",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "ExportArtifactId",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "ExportArtifactVersion",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "ExportConfirmationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "ExportConfirmedAtUtc",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "ExportConfirmedBy",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "ExportConfirmedOperationRevision",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "ExportFragmentSetSha256",
                schema: "data-rights",
                table: "tenant_termination_processes");

            migrationBuilder.DropColumn(
                name: "ExportFrozenRevisionSha256",
                schema: "data-rights",
                table: "tenant_termination_processes");
        }
    }
}
