using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTerminationCoordinatorFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_termination_processes",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    TerminationEpoch = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportRequested = table.Column<bool>(type: "boolean", nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    OutcomeCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HoldReviewAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_termination_processes", x => x.Id);
                    table.UniqueConstraint("AK_tenant_termination_processes_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.UniqueConstraint("AK_tenant_termination_processes_ScopeId_Id_CaseId_ApprovalRevi~", x => new { x.ScopeId, x.Id, x.CaseId, x.ApprovalRevision, x.TerminationEpoch, x.PolicyEvidenceSha256 });
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_approval", "\"ApprovalRevision\" >= 1 AND char_length(\"PolicyEvidenceSha256\") = 64 AND \"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$' AND length(trim(\"ApprovedBy\")) > 0 AND \"ApprovedAtUtc\" <= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_attribution", "length(trim(\"CreatedBy\")) > 0 AND length(trim(\"LastChangedBy\")) > 0");
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_completion", "(\"Phase\" = 5 AND \"Status\" = 5) OR (\"Phase\" BETWEEN 1 AND 4 AND \"Status\" BETWEEN 1 AND 4)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_operation", "\"OperationRevision\" >= 0 AND ((\"Status\" = 1 AND \"OperationRevision\" >= 0) OR (\"Status\" BETWEEN 2 AND 5 AND \"OperationRevision\" >= 1))");
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_outcome", "(\"Status\" = 3 AND \"OutcomeCode\" IS NOT NULL AND \"HoldReviewAtUtc\" IS NOT NULL AND \"HoldReviewAtUtc\" >= \"LastChangedAtUtc\") OR (\"Status\" = 4 AND \"OutcomeCode\" IS NOT NULL AND \"HoldReviewAtUtc\" IS NULL) OR (\"Status\" IN (1, 2, 5) AND \"OutcomeCode\" IS NULL AND \"HoldReviewAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_outcome_code", "\"OutcomeCode\" IS NULL OR \"OutcomeCode\" ~ '^[a-z0-9][a-z0-9._-]{0,199}$'");
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_phase", "\"Phase\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_status", "\"Status\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_timestamps", "\"LastChangedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_data_rights_tenant_termination_process_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_tenant_termination_processes_cases_ScopeId_CaseId",
                        columns: x => new { x.ScopeId, x.CaseId },
                        principalSchema: "data-rights",
                        principalTable: "cases",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_termination_owner_work_items",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    OperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    TerminationEpoch = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerContractVersion = table.Column<int>(type: "integer", nullable: false),
                    CatalogVersion = table.Column<int>(type: "integer", nullable: false),
                    CatalogSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    TaskRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastTaskAttempt = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AffectedCount = table.Column<long>(type: "bigint", nullable: true),
                    RetainedMinimumCount = table.Column<long>(type: "bigint", nullable: true),
                    RemainingActiveCount = table.Column<long>(type: "bigint", nullable: true),
                    HoldReviewAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SelectedProofRevision = table.Column<long>(type: "bigint", nullable: true),
                    ResultingProofRevision = table.Column<long>(type: "bigint", nullable: true),
                    ResultRecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_termination_owner_work_items", x => x.Id);
                    table.UniqueConstraint("AK_tenant_termination_owner_work_items_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_data_rights_tenant_termination_owner_attempt", "\"AttemptCount\" >= 0 AND \"LastTaskAttempt\" >= 0 AND ((\"State\" = 1 AND \"TaskRunId\" IS NULL) OR (\"State\" BETWEEN 2 AND 6 AND \"TaskRunId\" IS NOT NULL AND \"AttemptCount\" >= 1 AND \"LastTaskAttempt\" >= 1))");
                    table.CheckConstraint("CK_data_rights_tenant_termination_owner_coordinates", "\"ApprovalRevision\" >= 1 AND \"OperationRevision\" >= 1 AND \"OwnerContractVersion\" >= 1 AND \"CatalogVersion\" >= 1 AND length(trim(\"OwnerKey\")) > 0 AND \"OwnerKey\" ~ '^[a-z0-9][a-z0-9._-]{0,99}$' AND char_length(\"CatalogSha256\") = 64 AND \"CatalogSha256\" ~ '^[0-9a-f]{64}$' AND char_length(\"PolicyEvidenceSha256\") = 64 AND \"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_data_rights_tenant_termination_owner_phase", "\"Phase\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_data_rights_tenant_termination_owner_result", "(\"State\" IN (1, 2) AND \"ResultCode\" IS NULL AND \"AffectedCount\" IS NULL AND \"RetainedMinimumCount\" IS NULL AND \"RemainingActiveCount\" IS NULL AND \"HoldReviewAtUtc\" IS NULL AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultRecordedAtUtc\" IS NULL) OR (\"State\" = 6 AND \"ResultCode\" IS NOT NULL AND \"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND \"RemainingActiveCount\" = 0 AND \"HoldReviewAtUtc\" IS NULL AND \"SelectedProofRevision\" >= 1 AND \"ResultingProofRevision\" >= \"SelectedProofRevision\" AND \"ResultRecordedAtUtc\" IS NOT NULL) OR (\"State\" = 4 AND \"ResultCode\" IS NOT NULL AND \"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND \"RemainingActiveCount\" >= 0 AND \"HoldReviewAtUtc\" >= \"ResultRecordedAtUtc\" AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultRecordedAtUtc\" IS NOT NULL) OR (\"State\" IN (3, 5) AND \"ResultCode\" IS NOT NULL AND \"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND \"RemainingActiveCount\" >= 0 AND \"HoldReviewAtUtc\" IS NULL AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultRecordedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_data_rights_tenant_termination_owner_result_code", "\"ResultCode\" IS NULL OR \"ResultCode\" ~ '^[a-z0-9][a-z0-9._-]{0,199}$'");
                    table.CheckConstraint("CK_data_rights_tenant_termination_owner_state", "\"State\" BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_data_rights_tenant_termination_owner_timestamps", "\"LastChangedAtUtc\" >= \"CreatedAtUtc\" AND (\"LastAttemptAtUtc\" IS NULL OR \"LastAttemptAtUtc\" BETWEEN \"CreatedAtUtc\" AND \"LastChangedAtUtc\") AND (\"ResultRecordedAtUtc\" IS NULL OR \"ResultRecordedAtUtc\" BETWEEN \"CreatedAtUtc\" AND \"LastChangedAtUtc\")");
                    table.CheckConstraint("CK_data_rights_tenant_termination_owner_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_tenant_termination_owner_work_items_tenant_termination_proc~",
                        columns: x => new { x.ScopeId, x.ProcessId, x.CaseId, x.ApprovalRevision, x.TerminationEpoch, x.PolicyEvidenceSha256 },
                        principalSchema: "data-rights",
                        principalTable: "tenant_termination_processes",
                        principalColumns: new[] { "ScopeId", "Id", "CaseId", "ApprovalRevision", "TerminationEpoch", "PolicyEvidenceSha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_owner_work_items_ScopeId_IdempotencyKey",
                schema: "data-rights",
                table: "tenant_termination_owner_work_items",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_owner_work_items_ScopeId_ProcessId_CaseI~",
                schema: "data-rights",
                table: "tenant_termination_owner_work_items",
                columns: new[] { "ScopeId", "ProcessId", "CaseId", "ApprovalRevision", "TerminationEpoch", "PolicyEvidenceSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_owner_work_items_ScopeId_ProcessId_Phas~1",
                schema: "data-rights",
                table: "tenant_termination_owner_work_items",
                columns: new[] { "ScopeId", "ProcessId", "Phase", "OperationRevision", "State", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_owner_work_items_ScopeId_ProcessId_Phase~",
                schema: "data-rights",
                table: "tenant_termination_owner_work_items",
                columns: new[] { "ScopeId", "ProcessId", "Phase", "OwnerKey", "OperationRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_processes_ScopeId",
                schema: "data-rights",
                table: "tenant_termination_processes",
                column: "ScopeId",
                unique: true,
                filter: "\"Status\" IN (1, 2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_processes_ScopeId_CaseId",
                schema: "data-rights",
                table: "tenant_termination_processes",
                columns: new[] { "ScopeId", "CaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_processes_ScopeId_IdempotencyKey",
                schema: "data-rights",
                table: "tenant_termination_processes",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_termination_processes_ScopeId_Status_Phase_LastChang~",
                schema: "data-rights",
                table: "tenant_termination_processes",
                columns: new[] { "ScopeId", "Status", "Phase", "LastChangedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_termination_owner_work_items",
                schema: "data-rights");

            migrationBuilder.DropTable(
                name: "tenant_termination_processes",
                schema: "data-rights");
        }
    }
}
