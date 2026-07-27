using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDataRightsCorrectionExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "correction_executions",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedCaseVersion = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionRevision = table.Column<long>(type: "bigint", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    FieldPolicyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExecutedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ReceiptContractVersion = table.Column<int>(type: "integer", nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentRecordVersion = table.Column<long>(type: "bigint", nullable: true),
                    ChangedFieldCount = table.Column<int>(type: "integer", nullable: true),
                    ChangedFieldsSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_correction_executions", x => x.Id);
                    table.UniqueConstraint("AK_correction_executions_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_data_rights_correction_executions_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_data_rights_correction_executions_coordinates", "\"PropertyId\" IS NOT NULL AND \"CaseId\" IS NOT NULL AND \"RecordId\" IS NOT NULL AND length(trim(\"OwnerKey\")) > 0 AND length(trim(\"RecordType\")) > 0 AND length(trim(\"FieldPolicyKey\")) > 0 AND length(trim(\"ExecutedBy\")) > 0");
                    table.CheckConstraint("CK_data_rights_correction_executions_revisions", "\"SelectedCaseVersion\" >= 1 AND \"ExecutionRevision\" = \"SelectedCaseVersion\" + 1 AND \"ApprovalRevision\" >= 1 AND \"SelectedRecordVersion\" >= 1 AND \"Version\" >= 1");
                    table.CheckConstraint("CK_data_rights_correction_executions_state", "(\"State\" = 1 AND \"ReceiptContractVersion\" IS NULL AND \"ReceiptId\" IS NULL AND \"CurrentRecordVersion\" IS NULL AND \"ChangedFieldCount\" IS NULL AND \"ChangedFieldsSha256\" IS NULL AND \"ReceiptSha256\" IS NULL AND \"CompletedAtUtc\" IS NULL) OR (\"State\" = 2 AND \"ReceiptContractVersion\" >= 1 AND \"ReceiptId\" IS NOT NULL AND \"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1 AND \"ChangedFieldCount\" BETWEEN 1 AND 32 AND char_length(\"ChangedFieldsSha256\") = 64 AND char_length(\"ReceiptSha256\") = 64 AND \"CompletedAtUtc\" BETWEEN \"StartedAtUtc\" AND \"ExpiresAtUtc\")");
                    table.CheckConstraint("CK_data_rights_correction_executions_timestamps", "\"StartedAtUtc\" IS NOT NULL AND \"ExpiresAtUtc\" > \"StartedAtUtc\" AND \"ExpiresAtUtc\" <= \"StartedAtUtc\" + INTERVAL '15 minutes'");
                    table.ForeignKey(
                        name: "FK_correction_executions_cases_ScopeId_CaseId",
                        columns: x => new { x.ScopeId, x.CaseId },
                        principalSchema: "data-rights",
                        principalTable: "cases",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_correction_executions_ScopeId_CaseId",
                schema: "data-rights",
                table: "correction_executions",
                columns: new[] { "ScopeId", "CaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_correction_executions_ScopeId_PropertyId_State_ExpiresAtUtc",
                schema: "data-rights",
                table: "correction_executions",
                columns: new[] { "ScopeId", "PropertyId", "State", "ExpiresAtUtc" });
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
                        FROM "data-rights"."correction_executions")
                    THEN
                        RAISE EXCEPTION
                            'Cannot remove correction execution proof while correction executions exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "correction_executions",
                schema: "data-rights");
        }
    }
}
