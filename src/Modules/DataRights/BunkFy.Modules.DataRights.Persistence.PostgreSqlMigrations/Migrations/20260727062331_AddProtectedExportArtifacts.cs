using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddProtectedExportArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "export_artifacts",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CaseKind = table.Column<int>(type: "integer", nullable: false),
                    DecisionRevision = table.Column<long>(type: "bigint", nullable: false),
                    SelectedSubjectCount = table.Column<int>(type: "integer", nullable: false),
                    SelectionSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GenerationActor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GenerationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    GenerationAttempt = table.Column<int>(type: "integer", nullable: true),
                    GenerationStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EncryptedByteLength = table.Column<long>(type: "bigint", nullable: true),
                    PlaintextSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    EncryptionKeyVersion = table.Column<int>(type: "integer", nullable: true),
                    FormatVersion = table.Column<int>(type: "integer", nullable: true),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletionRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_export_artifacts", x => x.Id);
                    table.UniqueConstraint("AK_export_artifacts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_data_rights_export_artifacts_failure_shape", "(\"State\" = 4 AND \"FailureCode\" IS NOT NULL) OR (\"State\" <> 4 AND \"FailureCode\" IS NULL)");
                    table.CheckConstraint("CK_data_rights_export_artifacts_generation_shape", "(\"GenerationActor\" IS NULL AND \"GenerationRunId\" IS NULL AND \"GenerationAttempt\" IS NULL AND \"GenerationStartedAtUtc\" IS NULL) OR (\"GenerationActor\" IS NOT NULL AND \"GenerationRunId\" IS NOT NULL AND \"GenerationAttempt\" > 0 AND \"GenerationStartedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_export_artifacts_lifecycle_shape", "(\"State\" = 1 AND \"GenerationActor\" IS NULL AND \"StorageKey\" IS NULL AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 2 AND \"GenerationActor\" IS NOT NULL AND \"GenerationRunId\" IS NOT NULL AND \"GenerationAttempt\" > 0 AND \"GenerationStartedAtUtc\" IS NOT NULL AND \"StorageKey\" IS NULL AND \"EncryptedByteLength\" IS NULL AND \"PlaintextSha256\" IS NULL AND \"EncryptionKeyVersion\" IS NULL AND \"FormatVersion\" IS NULL AND \"AvailableAtUtc\" IS NULL AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 4 AND \"GenerationActor\" IS NOT NULL AND \"GenerationRunId\" IS NOT NULL AND \"GenerationAttempt\" > 0 AND \"GenerationStartedAtUtc\" IS NOT NULL AND \"StorageKey\" IS NULL AND \"FailureCode\" IS NOT NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 3 AND \"GenerationActor\" IS NOT NULL AND \"StorageKey\" IS NOT NULL AND \"EncryptedByteLength\" > 0 AND \"PlaintextSha256\" IS NOT NULL AND \"EncryptionKeyVersion\" > 0 AND \"FormatVersion\" > 0 AND \"AvailableAtUtc\" IS NOT NULL AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 5 AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NULL AND \"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 6 AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND \"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NULL) OR (\"State\" = 7 AND \"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND \"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_export_artifacts_revision", "\"DecisionRevision\" > 0");
                    table.CheckConstraint("CK_data_rights_export_artifacts_scope", "(\"CaseKind\" = 1 AND \"PropertyId\" IS NOT NULL) OR (\"CaseKind\" = 3 AND \"PropertyId\" IS NULL)");
                    table.CheckConstraint("CK_data_rights_export_artifacts_state", "\"State\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_data_rights_export_artifacts_storage_shape", "(\"StorageKey\" IS NULL AND \"EncryptedByteLength\" IS NULL AND \"PlaintextSha256\" IS NULL AND \"EncryptionKeyVersion\" IS NULL AND \"FormatVersion\" IS NULL AND \"AvailableAtUtc\" IS NULL) OR (\"GenerationActor\" IS NOT NULL AND \"StorageKey\" IS NOT NULL AND \"EncryptedByteLength\" > 0 AND \"PlaintextSha256\" IS NOT NULL AND \"EncryptionKeyVersion\" > 0 AND \"FormatVersion\" > 0 AND \"AvailableAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_export_artifacts_subject_count", "\"SelectedSubjectCount\" BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_data_rights_export_artifacts_timestamps", "\"ExpiresAtUtc\" > \"RequestedAtUtc\" AND (\"GenerationStartedAtUtc\" IS NULL OR (\"GenerationStartedAtUtc\" >= \"RequestedAtUtc\" AND \"GenerationStartedAtUtc\" < \"ExpiresAtUtc\")) AND (\"AvailableAtUtc\" IS NULL OR (\"GenerationStartedAtUtc\" IS NOT NULL AND \"AvailableAtUtc\" >= \"GenerationStartedAtUtc\" AND \"AvailableAtUtc\" < \"ExpiresAtUtc\")) AND (\"DeletionStartedAtUtc\" IS NULL OR \"DeletionStartedAtUtc\" >= \"ExpiresAtUtc\") AND (\"DeletedAtUtc\" IS NULL OR (\"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" >= \"DeletionStartedAtUtc\"))");
                    table.CheckConstraint("CK_data_rights_export_artifacts_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_export_artifacts_cases_ScopeId_CaseId",
                        columns: x => new { x.ScopeId, x.CaseId },
                        principalSchema: "data-rights",
                        principalTable: "cases",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "export_audit_entries",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CaseKind = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OutcomeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_export_audit_entries", x => x.Id);
                    table.UniqueConstraint("AK_export_audit_entries_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_data_rights_export_audit_action", "\"Action\" BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_data_rights_export_audit_scope", "(\"CaseKind\" = 1 AND \"PropertyId\" IS NOT NULL) OR (\"CaseKind\" = 3 AND \"PropertyId\" IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_export_artifacts_ScopeId_CaseId",
                schema: "data-rights",
                table: "export_artifacts",
                columns: new[] { "ScopeId", "CaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_export_artifacts_ScopeId_IdempotencyKey",
                schema: "data-rights",
                table: "export_artifacts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_export_artifacts_ScopeId_State_ExpiresAtUtc",
                schema: "data-rights",
                table: "export_artifacts",
                columns: new[] { "ScopeId", "State", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_export_audit_entries_ScopeId_ArtifactId_OccurredAtUtc_Id",
                schema: "data-rights",
                table: "export_audit_entries",
                columns: new[] { "ScopeId", "ArtifactId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_export_audit_entries_ScopeId_CaseId_OccurredAtUtc_Id",
                schema: "data-rights",
                table: "export_audit_entries",
                columns: new[] { "ScopeId", "CaseId", "OccurredAtUtc", "Id" });

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "data-rights".prevent_export_audit_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'data-rights export audit entries are append-only';
                END;
                $function$;

                CREATE TRIGGER "TR_export_audit_entries_append_only"
                BEFORE UPDATE OR DELETE ON "data-rights"."export_audit_entries"
                FOR EACH ROW
                EXECUTE FUNCTION "data-rights".prevent_export_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "export_artifacts",
                schema: "data-rights");

            migrationBuilder.DropTable(
                name: "export_audit_entries",
                schema: "data-rights");

            migrationBuilder.Sql(
                """
                DROP FUNCTION IF EXISTS "data-rights".prevent_export_audit_mutation();
                """);
        }
    }
}
