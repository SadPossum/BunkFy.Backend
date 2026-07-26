using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDataRightsExecutionBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                schema: "data-rights",
                table: "execution_work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "execution_batches",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionRevision = table.Column<long>(type: "bigint", nullable: false),
                    SelectedSubjectCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_batches", x => x.Id);
                    table.UniqueConstraint("AK_execution_batches_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_data_rights_execution_batches_created_by", "length(trim(\"CreatedBy\")) > 0");
                    table.CheckConstraint("CK_data_rights_execution_batches_revisions", "\"ApprovalRevision\" >= 1 AND \"ExecutionRevision\" > \"ApprovalRevision\"");
                    table.CheckConstraint("CK_data_rights_execution_batches_subject_count", "\"SelectedSubjectCount\" BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_data_rights_execution_batches_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_execution_batches_cases_ScopeId_CaseId",
                        columns: x => new { x.ScopeId, x.CaseId },
                        principalSchema: "data-rights",
                        principalTable: "cases",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "data-rights"."execution_batches" (
                    "Id",
                    "IdempotencyKey",
                    "CaseId",
                    "PropertyId",
                    "ApprovalRevision",
                    "ExecutionRevision",
                    "SelectedSubjectCount",
                    "CreatedBy",
                    "CreatedAtUtc",
                    "Version",
                    "ScopeId")
                SELECT
                    work_item."Id",
                    work_item."IdempotencyKey",
                    work_item."CaseId",
                    work_item."PropertyId",
                    work_item."ApprovalRevision",
                    work_item."ExecutionRevision",
                    1,
                    work_item."CreatedBy",
                    work_item."CreatedAtUtc",
                    1,
                    work_item."ScopeId"
                FROM "data-rights"."execution_work_items" AS work_item;

                UPDATE "data-rights"."execution_work_items"
                SET "BatchId" = "Id";

                UPDATE "data-rights"."execution_work_items" AS work_item
                SET
                    "State" = 5,
                    "Version" = work_item."Version" + 1
                WHERE work_item."State" = 7
                  AND EXISTS (
                    SELECT 1
                    FROM "data-rights"."processing_ledger_entries" AS ledger
                    WHERE ledger."ScopeId" = work_item."ScopeId"
                      AND ledger."WorkItemId" = work_item."Id"
                      AND ledger."CaseId" = work_item."CaseId"
                      AND ledger."OperationRevision" = work_item."ExecutionRevision"
                      AND ledger."OwnerReceiptContractVersion" =
                          work_item."OwnerReceiptContractVersion"
                      AND ledger."OwnerReceiptId" = work_item."OwnerReceiptId"
                      AND ledger."OwnerReceiptSha256" =
                          work_item."OwnerReceiptSha256");

                UPDATE "data-rights"."cases" AS data_rights_case
                SET
                    "Status" = 9,
                    "LastChangedBy" = 'system:data-rights-migration',
                    "LastChangedAtUtc" = GREATEST(
                        data_rights_case."LastChangedAtUtc",
                        work_item."OutcomeAtUtc"),
                    "Version" = data_rights_case."Version" + 1
                FROM "data-rights"."execution_work_items" AS work_item
                WHERE data_rights_case."ScopeId" = work_item."ScopeId"
                  AND data_rights_case."Id" = work_item."CaseId"
                  AND data_rights_case."Status" = 7
                  AND work_item."State" = 5;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "BatchId",
                schema: "data-rights",
                table: "execution_work_items",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_work_items_ScopeId_BatchId_OwnerKey_RecordType_Re~",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "BatchId", "OwnerKey", "RecordType", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_batches_ScopeId_CaseId",
                schema: "data-rights",
                table: "execution_batches",
                columns: new[] { "ScopeId", "CaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_batches_ScopeId_IdempotencyKey",
                schema: "data-rights",
                table: "execution_batches",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_execution_work_items_execution_batches_ScopeId_BatchId",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "BatchId" },
                principalSchema: "data-rights",
                principalTable: "execution_batches",
                principalColumns: new[] { "ScopeId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_execution_work_items_execution_batches_ScopeId_BatchId",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."execution_batches" AS batch
                        LEFT JOIN "data-rights"."execution_work_items" AS work_item
                          ON work_item."ScopeId" = batch."ScopeId"
                         AND work_item."BatchId" = batch."Id"
                        GROUP BY batch."ScopeId", batch."Id", batch."SelectedSubjectCount"
                        HAVING batch."SelectedSubjectCount" <> 1
                            OR COUNT(work_item."Id") <> 1)
                    THEN
                        RAISE EXCEPTION
                            'Cannot downgrade data-rights execution batches with multi-owner or incomplete state.';
                    END IF;
                END $$;

                UPDATE "data-rights"."execution_work_items" AS work_item
                SET "IdempotencyKey" = batch."IdempotencyKey"
                FROM "data-rights"."execution_batches" AS batch
                WHERE work_item."ScopeId" = batch."ScopeId"
                  AND work_item."BatchId" = batch."Id";
                """);

            migrationBuilder.DropTable(
                name: "execution_batches",
                schema: "data-rights");

            migrationBuilder.DropIndex(
                name: "IX_execution_work_items_ScopeId_BatchId_OwnerKey_RecordType_Re~",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "BatchId",
                schema: "data-rights",
                table: "execution_work_items");
        }
    }
}
