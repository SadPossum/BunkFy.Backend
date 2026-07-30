using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceStaffRetentionCorrelationScrub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_retention_correlation_receipts",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    OnboardingRecordsScrubbed = table.Column<int>(type: "integer", nullable: false),
                    AccessProcessRecordsScrubbed = table.Column<int>(type: "integer", nullable: false),
                    AccessPlanRecordsScrubbed = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_retention_correlation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_retention_correlation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_retention_correlation_receipt_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_retention_correlation_receipt_counts", "\"OnboardingRecordsScrubbed\" >= 0 AND \"AccessProcessRecordsScrubbed\" >= 0 AND \"AccessPlanRecordsScrubbed\" >= 0");
                    table.CheckConstraint("CK_staff_retention_correlation_receipt_hash", "char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_staff_retention_correlation_receipt_version", "\"SelectedStaffVersion\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_applications_ScopeId_SubjectId_Status_Id",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                columns: new[] { "ScopeId", "SubjectId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_access_processes_ScopeId_RequestedBy_Id",
                schema: "workspaces",
                table: "staff_access_processes",
                columns: new[] { "ScopeId", "RequestedBy", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_access_processes_ScopeId_SubjectId_State_Id",
                schema: "workspaces",
                table: "staff_access_processes",
                columns: new[] { "ScopeId", "SubjectId", "State", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_access_plans_ScopeId_CreatedBySubjectId_Id",
                schema: "workspaces",
                table: "staff_access_plans",
                columns: new[] { "ScopeId", "CreatedBySubjectId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_retention_correlation_receipts_ScopeId_ExecutionId_Id",
                schema: "workspaces",
                table: "staff_retention_correlation_receipts",
                columns: new[] { "ScopeId", "ExecutionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_retention_correlation_receipts_ScopeId_StaffMemberId_~",
                schema: "workspaces",
                table: "staff_retention_correlation_receipts",
                columns: new[] { "ScopeId", "StaffMemberId", "SelectedStaffVersion" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "workspaces".prevent_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'workspace receipts are append-only';
                END;
                $$;

                CREATE TRIGGER "TR_staff_retention_correlation_receipts_append_only"
                BEFORE UPDATE OR DELETE
                ON "workspaces"."staff_retention_correlation_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "workspaces".prevent_receipt_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_staff_retention_correlation_receipts_append_only"
                    ON "workspaces"."staff_retention_correlation_receipts";
                DROP FUNCTION IF EXISTS
                    "workspaces".prevent_receipt_mutation();
                """);

            migrationBuilder.DropTable(
                name: "staff_retention_correlation_receipts",
                schema: "workspaces");

            migrationBuilder.DropIndex(
                name: "IX_staff_onboarding_applications_ScopeId_SubjectId_Status_Id",
                schema: "workspaces",
                table: "staff_onboarding_applications");

            migrationBuilder.DropIndex(
                name: "IX_staff_access_processes_ScopeId_RequestedBy_Id",
                schema: "workspaces",
                table: "staff_access_processes");

            migrationBuilder.DropIndex(
                name: "IX_staff_access_processes_ScopeId_SubjectId_State_Id",
                schema: "workspaces",
                table: "staff_access_processes");

            migrationBuilder.DropIndex(
                name: "IX_staff_access_plans_ScopeId_CreatedBySubjectId_Id",
                schema: "workspaces",
                table: "staff_access_plans");
        }
    }
}
