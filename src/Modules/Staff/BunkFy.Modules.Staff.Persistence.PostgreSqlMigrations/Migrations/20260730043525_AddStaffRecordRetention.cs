using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffRecordRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_authority",
                schema: "staff",
                table: "staff_anonymisation_tombstones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_contract",
                schema: "staff",
                table: "staff_anonymisation_tombstones");

            migrationBuilder.Sql(
                """
                UPDATE staff.staff_anonymisation_tombstones
                SET "ContractVersion" = 3
                WHERE "ContractVersion" = 2
                  AND "Authority" = 1;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM staff.staff_anonymisation_tombstones
                        WHERE "ContractVersion" <> 3
                           OR "Authority" <> 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot upgrade Staff because existing anonymisation tombstone authority or contract is unsupported.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.CreateTable(
                name: "staff_retention_executions",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataClassKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExecutionPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    StartingProjectionOrdinal = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AffectedCount = table.Column<int>(type: "integer", nullable: false),
                    ScannedCount = table.Column<int>(type: "integer", nullable: true),
                    RemainingCount = table.Column<int>(type: "integer", nullable: true),
                    OutcomeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HoldReviewDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_retention_executions", x => x.Id);
                    table.UniqueConstraint("AK_staff_retention_executions_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_retention_executions_counts", "\"AffectedCount\" >= 0 AND (\"ScannedCount\" IS NULL OR \"ScannedCount\" >= 0) AND (\"RemainingCount\" IS NULL OR \"RemainingCount\" >= 0) AND (\"ScannedCount\" IS NULL OR \"AffectedCount\" <= \"ScannedCount\")");
                    table.CheckConstraint("CK_staff_retention_executions_cursor", "\"StartingProjectionOrdinal\" >= 0");
                    table.CheckConstraint("CK_staff_retention_executions_key", "\"DataClassKey\" ~ '^[A-Za-z0-9.-]+$'");
                    table.CheckConstraint("CK_staff_retention_executions_policy", "\"ExecutionPolicyVersion\" >= 1 AND \"Attempt\" >= 1 AND \"DeadlineUtc\" > \"StartedAtUtc\"");
                    table.CheckConstraint("CK_staff_retention_executions_state", "(\"State\" = 1 AND \"CompletedAtUtc\" IS NULL AND \"ScannedCount\" IS NULL AND \"RemainingCount\" IS NULL AND \"OutcomeCode\" IS NULL AND \"HoldReviewDueAtUtc\" IS NULL) OR (\"State\" IN (2, 3, 4) AND \"CompletedAtUtc\" BETWEEN \"StartedAtUtc\" AND \"DeadlineUtc\" AND \"ScannedCount\" IS NOT NULL AND \"RemainingCount\" IS NOT NULL AND \"OutcomeCode\" ~ '^[A-Za-z0-9.-]+$' AND ((\"State\" = 3 AND \"HoldReviewDueAtUtc\" IS NOT NULL) OR (\"State\" <> 3 AND \"HoldReviewDueAtUtc\" IS NULL)))");
                    table.CheckConstraint("CK_staff_retention_executions_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "staff_retention_sweep_checkpoints",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataClassKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExecutionPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    AfterProjectionOrdinal = table.Column<long>(type: "bigint", nullable: false),
                    LastExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_retention_sweep_checkpoints", x => x.Id);
                    table.UniqueConstraint("AK_staff_retention_sweep_checkpoints_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_retention_checkpoints_cursor", "\"AfterProjectionOrdinal\" >= 0");
                    table.CheckConstraint("CK_staff_retention_checkpoints_key", "\"DataClassKey\" ~ '^[A-Za-z0-9.-]+$'");
                    table.CheckConstraint("CK_staff_retention_checkpoints_policy", "\"ExecutionPolicyVersion\" >= 1");
                    table.CheckConstraint("CK_staff_retention_checkpoints_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "staff_retention_anonymisation_receipts",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    SelectedOperationLockRevision = table.Column<long>(type: "bigint", nullable: false),
                    ResultingOperationLockRevision = table.Column<long>(type: "bigint", nullable: false),
                    DepartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetentionDeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_retention_anonymisation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_retention_anonymisation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_retention_receipts_actor", "\"ActorId\" = 'system:retention'");
                    table.CheckConstraint("CK_staff_retention_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_retention_receipts_deadline", "\"RetentionDeadlineUtc\" >= \"DepartedAtUtc\" AND \"CompletedAtUtc\" >= \"RetentionDeadlineUtc\"");
                    table.CheckConstraint("CK_staff_retention_receipts_digests", "char_length(\"PolicyEvidenceSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_staff_retention_receipts_versions", "\"SelectedStaffVersion\" >= 1 AND \"ResultingStaffVersion\" = \"SelectedStaffVersion\" + 1 AND \"SelectedOperationLockRevision\" >= 1 AND \"ResultingOperationLockRevision\" = \"SelectedOperationLockRevision\" + 1");
                    table.ForeignKey(
                        name: "FK_staff_retention_anonymisation_receipts_staff_members_ScopeI~",
                        columns: x => new { x.ScopeId, x.StaffMemberId },
                        principalSchema: "staff",
                        principalTable: "staff_members",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_retention_anonymisation_receipts_staff_retention_exec~",
                        columns: x => new { x.ScopeId, x.ExecutionId },
                        principalSchema: "staff",
                        principalTable: "staff_retention_executions",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_members_ScopeId_Status_ProjectionOrdinal_Id",
                schema: "staff",
                table: "staff_members",
                columns: new[] { "ScopeId", "Status", "ProjectionOrdinal", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_authority",
                schema: "staff",
                table: "staff_anonymisation_tombstones",
                sql: "\"Authority\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_contract",
                schema: "staff",
                table: "staff_anonymisation_tombstones",
                sql: "\"ContractVersion\" = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_restore_proof",
                schema: "staff",
                table: "staff_anonymisation_tombstones",
                sql: "\"Authority\" <> 2 OR (\"LedgerEntryId\" IS NULL AND \"LastReplayedAtUtc\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_staff_retention_anonymisation_receipts_ScopeId_ExecutionId_~",
                schema: "staff",
                table: "staff_retention_anonymisation_receipts",
                columns: new[] { "ScopeId", "ExecutionId", "StaffMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_retention_anonymisation_receipts_ScopeId_StaffMemberId",
                schema: "staff",
                table: "staff_retention_anonymisation_receipts",
                columns: new[] { "ScopeId", "StaffMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_retention_executions_ScopeId_DataClassKey_CompletedAt~",
                schema: "staff",
                table: "staff_retention_executions",
                columns: new[] { "ScopeId", "DataClassKey", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_retention_sweep_checkpoints_ScopeId_DataClassKey_Exec~",
                schema: "staff",
                table: "staff_retention_sweep_checkpoints",
                columns: new[] { "ScopeId", "DataClassKey", "ExecutionPolicyVersion" },
                unique: true);
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
                        FROM staff.staff_retention_anonymisation_receipts
                    ) OR EXISTS (
                        SELECT 1
                        FROM staff.staff_anonymisation_tombstones
                        WHERE "Authority" <> 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade Staff while retention anonymisation proof exists.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "staff_retention_anonymisation_receipts",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_retention_sweep_checkpoints",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_retention_executions",
                schema: "staff");

            migrationBuilder.DropIndex(
                name: "IX_staff_members_ScopeId_Status_ProjectionOrdinal_Id",
                schema: "staff",
                table: "staff_members");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_authority",
                schema: "staff",
                table: "staff_anonymisation_tombstones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_contract",
                schema: "staff",
                table: "staff_anonymisation_tombstones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_restore_proof",
                schema: "staff",
                table: "staff_anonymisation_tombstones");

            migrationBuilder.Sql(
                """
                UPDATE staff.staff_anonymisation_tombstones
                SET "ContractVersion" = 2
                WHERE "Authority" = 1;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_authority",
                schema: "staff",
                table: "staff_anonymisation_tombstones",
                sql: "\"Authority\" = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_anonymisation_tombstones_contract",
                schema: "staff",
                table: "staff_anonymisation_tombstones",
                sql: "\"ContractVersion\" = 2");
        }
    }
}
