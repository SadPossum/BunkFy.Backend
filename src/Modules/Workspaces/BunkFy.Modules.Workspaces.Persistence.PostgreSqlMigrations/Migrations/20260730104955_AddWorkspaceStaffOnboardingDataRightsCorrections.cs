using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceStaffOnboardingDataRightsCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_onboarding_correction_receipts",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    CurrentRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    ChangedFieldsMask = table.Column<int>(type: "integer", nullable: false),
                    RequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ApplicantEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletionEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_onboarding_correction_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_onboarding_correction_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_workspaces_staff_onboarding_correction_receipts_approval", "\"ApprovalRevision\" >= 1");
                    table.CheckConstraint("CK_workspaces_staff_onboarding_correction_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_workspaces_staff_onboarding_correction_receipts_digest", "char_length(\"RequestSha256\") = 64");
                    table.CheckConstraint("CK_workspaces_staff_onboarding_correction_receipts_fields", "\"ChangedFieldsMask\" BETWEEN 1 AND 127");
                    table.CheckConstraint("CK_workspaces_staff_onboarding_correction_receipts_versions", "\"SelectedRecordVersion\" >= 1 AND \"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntil~",
                schema: "workspaces",
                table: "outbox_messages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_correction_receipts_ScopeId_ApplicantEvent~",
                schema: "workspaces",
                table: "staff_onboarding_correction_receipts",
                columns: new[] { "ScopeId", "ApplicantEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_correction_receipts_ScopeId_ApplicationId_~",
                schema: "workspaces",
                table: "staff_onboarding_correction_receipts",
                columns: new[] { "ScopeId", "ApplicationId", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_correction_receipts_ScopeId_CaseId_Approva~",
                schema: "workspaces",
                table: "staff_onboarding_correction_receipts",
                columns: new[] { "ScopeId", "CaseId", "ApprovalRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_correction_receipts_ScopeId_CompletionEven~",
                schema: "workspaces",
                table: "staff_onboarding_correction_receipts",
                columns: new[] { "ScopeId", "CompletionEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_correction_receipts_ScopeId_ExecutionId",
                schema: "workspaces",
                table: "staff_onboarding_correction_receipts",
                columns: new[] { "ScopeId", "ExecutionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "staff_onboarding_correction_receipts",
                schema: "workspaces");
        }
    }
}
