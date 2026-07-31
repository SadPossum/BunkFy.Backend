using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceTerminationFence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_termination_fences",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    TerminationEpoch = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_termination_fences", x => x.Id);
                    table.UniqueConstraint("AK_workspace_termination_fences_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_workspace_termination_fence_policy_digest", "char_length(\"PolicyEvidenceSha256\") = 64");
                    table.CheckConstraint("CK_workspace_termination_fence_revisions", "\"ApprovalRevision\" >= 1 AND \"Version\" >= 1");
                    table.CheckConstraint("CK_workspace_termination_fence_state", "\"State\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_workspace_termination_fence_timestamps", "\"CreatedAtUtc\" <= \"LastChangedAtUtc\"");
                });

            migrationBuilder.CreateTable(
                name: "workspace_termination_fence_receipts",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    OperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    TerminationEpoch = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    SelectedFenceVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingFenceVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingState = table.Column<int>(type: "integer", nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_termination_fence_receipts", x => x.Id);
                    table.UniqueConstraint("AK_workspace_termination_fence_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_workspace_termination_receipt_policy_digest", "char_length(\"PolicyEvidenceSha256\") = 64");
                    table.CheckConstraint("CK_workspace_termination_receipt_revisions", "\"ApprovalRevision\" >= 1 AND \"OperationRevision\" >= 1 AND ((\"Action\" = 1 AND \"SelectedFenceVersion\" = 0 AND \"ResultingFenceVersion\" = 1 AND \"ResultingState\" = 1) OR (\"Action\" = 2 AND \"SelectedFenceVersion\" >= 1 AND \"ResultingFenceVersion\" = \"SelectedFenceVersion\" + 1 AND \"ResultingState\" = 2) OR (\"Action\" = 3 AND \"SelectedFenceVersion\" >= 2 AND \"ResultingFenceVersion\" = \"SelectedFenceVersion\" + 1 AND \"ResultingState\" = 3) OR (\"Action\" = 4 AND \"SelectedFenceVersion\" >= 1 AND \"ResultingFenceVersion\" = \"SelectedFenceVersion\" + 1 AND \"ResultingState\" = 4))");
                    table.ForeignKey(
                        name: "FK_workspace_termination_fence_receipts_workspace_termination_~",
                        columns: x => new { x.ScopeId, x.FenceId },
                        principalSchema: "workspaces",
                        principalTable: "workspace_termination_fences",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_termination_fence_receipts_ScopeId_FenceId",
                schema: "workspaces",
                table: "workspace_termination_fence_receipts",
                columns: new[] { "ScopeId", "FenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_termination_fence_receipts_ScopeId_IdempotencyKey",
                schema: "workspaces",
                table: "workspace_termination_fence_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_termination_fence_receipts_ScopeId_ProcessId_Acti~",
                schema: "workspaces",
                table: "workspace_termination_fence_receipts",
                columns: new[] { "ScopeId", "ProcessId", "Action", "OperationRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_termination_fence_receipts_ScopeId_ProcessId_Comp~",
                schema: "workspaces",
                table: "workspace_termination_fence_receipts",
                columns: new[] { "ScopeId", "ProcessId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_termination_fences_ScopeId_ProcessId",
                schema: "workspaces",
                table: "workspace_termination_fences",
                columns: new[] { "ScopeId", "ProcessId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_termination_fences_ScopeId_TerminationEpoch",
                schema: "workspaces",
                table: "workspace_termination_fences",
                columns: new[] { "ScopeId", "TerminationEpoch" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_workspace_termination_fences_active_scope",
                schema: "workspaces",
                table: "workspace_termination_fences",
                column: "ScopeId",
                unique: true,
                filter: "\"State\" IN (1, 2, 3)");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION
                    "workspaces".prevent_termination_fence_deletion()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'workspace termination fences are historical evidence';
                END;
                $$;

                CREATE TRIGGER
                    "TR_workspace_termination_fence_receipts_append_only"
                BEFORE UPDATE OR DELETE
                ON "workspaces"."workspace_termination_fence_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "workspaces".prevent_receipt_mutation();

                CREATE TRIGGER
                    "TR_workspace_termination_fences_no_delete"
                BEFORE DELETE
                ON "workspaces"."workspace_termination_fences"
                FOR EACH ROW
                EXECUTE FUNCTION
                    "workspaces".prevent_termination_fence_deletion();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_workspace_termination_fence_receipts_append_only"
                    ON
                    "workspaces"."workspace_termination_fence_receipts";
                DROP TRIGGER IF EXISTS
                    "TR_workspace_termination_fences_no_delete"
                    ON "workspaces"."workspace_termination_fences";
                DROP FUNCTION IF EXISTS
                    "workspaces".prevent_termination_fence_deletion();
                """);

            migrationBuilder.DropTable(
                name: "workspace_termination_fence_receipts",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "workspace_termination_fences",
                schema: "workspaces");
        }
    }
}
