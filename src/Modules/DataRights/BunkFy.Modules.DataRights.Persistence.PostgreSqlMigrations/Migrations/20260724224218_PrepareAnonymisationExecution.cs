using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class PrepareAnonymisationExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."cases"
                        WHERE "Status" IN (7, 8, 9, 10))
                    THEN
                        RAISE EXCEPTION
                            'Cannot add execution evidence while legacy cases are already executing or resolved.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_decision_state",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddColumn<long>(
                name: "ExecutionRevision",
                schema: "data-rights",
                table: "cases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExecutionStartedAtUtc",
                schema: "data-rights",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionStartedBy",
                schema: "data-rights",
                table: "cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "execution_work_items",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionRevision = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<int>(type: "integer", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    PolicyEvidenceSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    PolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    RetentionPolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RetentionPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    PolicyContentSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_work_items", x => x.Id);
                    table.CheckConstraint("CK_data_rights_execution_work_items_attempts", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_data_rights_execution_work_items_created_by", "length(trim(\"CreatedBy\")) > 0");
                    table.CheckConstraint("CK_data_rights_execution_work_items_operation", "\"Operation\" = 16");
                    table.CheckConstraint("CK_data_rights_execution_work_items_policy", "\"PolicyEvidenceSchemaVersion\" = 1 AND length(trim(\"PolicyId\")) > 0 AND \"PolicyVersion\" >= 1 AND length(trim(\"RetentionPolicyId\")) > 0 AND \"RetentionPolicyVersion\" >= 1 AND char_length(\"PolicyContentSha256\") = 64");
                    table.CheckConstraint("CK_data_rights_execution_work_items_revisions", "\"ApprovalRevision\" >= 1 AND \"ExecutionRevision\" > \"ApprovalRevision\"");
                    table.CheckConstraint("CK_data_rights_execution_work_items_state", "\"State\" BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_data_rights_execution_work_items_subject", "length(trim(\"OwnerKey\")) > 0 AND length(trim(\"RecordType\")) > 0 AND \"SelectedRecordVersion\" >= 1");
                    table.CheckConstraint("CK_data_rights_execution_work_items_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_execution_work_items_cases_ScopeId_CaseId",
                        columns: x => new { x.ScopeId, x.CaseId },
                        principalSchema: "data-rights",
                        principalTable: "cases",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_decision_state",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Decision\" = 0 AND \"Status\" IN (1, 2, 3, 4, 11)) OR (\"Decision\" = 1 AND \"Status\" IN (5, 7, 8, 9, 10, 11)) OR (\"Decision\" = 2 AND \"Status\" = 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_execution",
                schema: "data-rights",
                table: "cases",
                sql: "(\"ExecutionRevision\" IS NULL AND \"ExecutionStartedBy\" IS NULL AND \"ExecutionStartedAtUtc\" IS NULL AND \"Status\" IN (1, 2, 3, 4, 5, 6, 11)) OR (\"ExecutionRevision\" IS NOT NULL AND \"ExecutionRevision\" > \"DecisionRevision\" AND \"ExecutionRevision\" <= \"Version\" AND \"ExecutionStartedBy\" IS NOT NULL AND \"ExecutionStartedAtUtc\" IS NOT NULL AND \"Decision\" = 1 AND \"Status\" IN (7, 8, 9, 10, 11))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_execution_attribution",
                schema: "data-rights",
                table: "cases",
                sql: "\"ExecutionStartedBy\" IS NULL OR (length(trim(\"ExecutionStartedBy\")) > 0 AND \"ExecutionStartedAtUtc\" >= \"DecidedAtUtc\" AND \"ExecutionStartedAtUtc\" <= \"LastChangedAtUtc\")");

            migrationBuilder.CreateIndex(
                name: "IX_execution_work_items_ScopeId_CaseId_ApprovalRevision_Operat~",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "CaseId", "ApprovalRevision", "Operation", "OwnerKey", "RecordType", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_work_items_ScopeId_IdempotencyKey",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_work_items_ScopeId_PropertyId_State_CreatedAtUtc_~",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "PropertyId", "State", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execution_work_items",
                schema: "data-rights");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_decision_state",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_execution",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_execution_attribution",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ExecutionRevision",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ExecutionStartedAtUtc",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ExecutionStartedBy",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_decision_state",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Decision\" = 0 AND \"Status\" IN (1, 2, 3, 4, 11)) OR (\"Decision\" = 1 AND \"Status\" IN (5, 7, 8, 9, 10)) OR (\"Decision\" = 2 AND \"Status\" = 6)");
        }
    }
}
