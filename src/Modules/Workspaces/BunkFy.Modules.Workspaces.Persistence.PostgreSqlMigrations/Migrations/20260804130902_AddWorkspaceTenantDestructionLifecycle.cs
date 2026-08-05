using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceTenantDestructionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_destroy_operations",
                schema: "workspaces",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    FenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedFenceVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingFenceVersion = table.Column<long>(type: "bigint", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    RemovedRecordCount = table.Column<long>(type: "bigint", nullable: false),
                    CompletedBatchCount = table.Column<int>(type: "integer", nullable: false),
                    ProofVersion = table.Column<int>(type: "integer", nullable: false),
                    RemovalProofSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_destroy_operations", x => x.OperationId);
                    table.CheckConstraint("CK_workspaces_tenant_destroy_operation_batch", "\"BatchSize\" BETWEEN 1 AND 500");
                    table.CheckConstraint("CK_workspaces_tenant_destroy_operation_progress", "\"Stage\" BETWEEN 1 AND 20 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
                    table.CheckConstraint("CK_workspaces_tenant_destroy_operation_revisions", "\"SelectedFenceVersion\" >= 1 AND \"ResultingFenceVersion\" = \"SelectedFenceVersion\" + 2");
                    table.CheckConstraint("CK_workspaces_tenant_destroy_operation_times", "\"UpdatedAtUtc\" >= \"StartedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_tenant_destroy_operations_workspace_termination_fences_Scop~",
                        columns: x => new { x.ScopeId, x.FenceId },
                        principalSchema: "workspaces",
                        principalTable: "workspace_termination_fences",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_destroy_receipts",
                schema: "workspaces",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    FenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CloseFenceReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedFenceVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingFenceVersion = table.Column<long>(type: "bigint", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    RemovedRecordCount = table.Column<long>(type: "bigint", nullable: false),
                    CompletedBatchCount = table.Column<int>(type: "integer", nullable: false),
                    RemovalProofVersion = table.Column<int>(type: "integer", nullable: false),
                    RemovalProofSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_destroy_receipts", x => x.OperationId);
                    table.CheckConstraint("CK_workspaces_tenant_destroy_receipt_progress", "((\"RemovedRecordCount\" = 0 AND \"CompletedBatchCount\" = 0) OR (\"RemovedRecordCount\" > 0 AND \"CompletedBatchCount\" > 0)) AND \"BatchSize\" BETWEEN 1 AND 500 AND \"RemovalProofVersion\" = 1");
                    table.CheckConstraint("CK_workspaces_tenant_destroy_receipt_revisions", "\"SelectedFenceVersion\" >= 1 AND \"ResultingFenceVersion\" = \"SelectedFenceVersion\" + 2");
                    table.CheckConstraint("CK_workspaces_tenant_destroy_receipt_times", "\"CompletedAtUtc\" >= \"StartedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_tenant_destroy_receipts_workspace_termination_fence_receipt~",
                        columns: x => new { x.ScopeId, x.CloseFenceReceiptId },
                        principalSchema: "workspaces",
                        principalTable: "workspace_termination_fence_receipts",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_destroy_receipts_workspace_termination_fences_ScopeI~",
                        columns: x => new { x.ScopeId, x.FenceId },
                        principalSchema: "workspaces",
                        principalTable: "workspace_termination_fences",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_destroy_operations_ScopeId",
                schema: "workspaces",
                table: "tenant_destroy_operations",
                column: "ScopeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_destroy_operations_ScopeId_FenceId",
                schema: "workspaces",
                table: "tenant_destroy_operations",
                columns: new[] { "ScopeId", "FenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_destroy_receipts_ScopeId",
                schema: "workspaces",
                table: "tenant_destroy_receipts",
                column: "ScopeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_destroy_receipts_ScopeId_CloseFenceReceiptId",
                schema: "workspaces",
                table: "tenant_destroy_receipts",
                columns: new[] { "ScopeId", "CloseFenceReceiptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_destroy_receipts_ScopeId_FenceId",
                schema: "workspaces",
                table: "tenant_destroy_receipts",
                columns: new[] { "ScopeId", "FenceId" });

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION
                    "workspaces".prevent_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    destroy_operation_id text;
                BEGIN
                    destroy_operation_id := current_setting(
                        'bunkfy.workspaces_tenant_destroy_operation_id',
                        true);
                    IF TG_OP = 'DELETE' AND
                       destroy_operation_id IS NOT NULL AND
                       EXISTS (
                           SELECT 1
                           FROM "workspaces"."tenant_destroy_operations" operation
                           INNER JOIN
                               "workspaces"."workspace_termination_fences" fence
                               ON fence."ScopeId" = operation."ScopeId"
                              AND fence."Id" = operation."FenceId"
                           WHERE operation."OperationId"::text =
                                     destroy_operation_id
                             AND operation."ScopeId" = OLD."ScopeId"
                             AND fence."State" = 2)
                    THEN
                        RETURN OLD;
                    END IF;

                    RAISE EXCEPTION 'workspace receipts are append-only';
                END;
                $function$;

                CREATE OR REPLACE FUNCTION
                    "workspaces".prevent_termination_fence_deletion()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    destroy_operation_id text;
                BEGIN
                    destroy_operation_id := current_setting(
                        'bunkfy.workspaces_tenant_destroy_operation_id',
                        true);
                    IF destroy_operation_id IS NOT NULL AND
                       EXISTS (
                           SELECT 1
                           FROM "workspaces"."tenant_destroy_operations" operation
                           INNER JOIN
                               "workspaces"."workspace_termination_fences" fence
                               ON fence."ScopeId" = operation."ScopeId"
                              AND fence."Id" = operation."FenceId"
                           WHERE operation."OperationId"::text =
                                     destroy_operation_id
                             AND operation."ScopeId" = OLD."ScopeId"
                             AND operation."FenceId" <> OLD."Id"
                             AND fence."State" = 2)
                    THEN
                        RETURN OLD;
                    END IF;

                    RAISE EXCEPTION
                        'workspace termination fences are historical evidence';
                END;
                $function$;

                CREATE FUNCTION
                    "workspaces".reject_tenant_destroy_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION
                        'workspace tenant destruction receipts are append-only';
                END;
                $function$;

                CREATE TRIGGER
                    "TR_tenant_destroy_receipts_append_only"
                BEFORE UPDATE OR DELETE
                ON "workspaces"."tenant_destroy_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION
                    "workspaces".reject_tenant_destroy_receipt_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_tenant_destroy_receipts_append_only"
                    ON "workspaces"."tenant_destroy_receipts";
                DROP FUNCTION IF EXISTS
                    "workspaces".reject_tenant_destroy_receipt_mutation();

                CREATE OR REPLACE FUNCTION
                    "workspaces".prevent_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'workspace receipts are append-only';
                END;
                $function$;

                CREATE OR REPLACE FUNCTION
                    "workspaces".prevent_termination_fence_deletion()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION
                        'workspace termination fences are historical evidence';
                END;
                $function$;
                """);

            migrationBuilder.DropTable(
                name: "tenant_destroy_operations",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "tenant_destroy_receipts",
                schema: "workspaces");
        }
    }
}
