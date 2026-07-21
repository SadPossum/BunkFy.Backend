using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkspaceStaffOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workspaces");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Handler = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.Id, x.Handler });
                });

            migrationBuilder.CreateTable(
                name: "staff_onboarding_applications",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimVersion = table.Column<long>(type: "bigint", nullable: true),
                    SubjectId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    VerifiedAccountEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LegalName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    WorkEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    WorkPhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EmployeeNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    JobTitle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Department = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_onboarding_applications", x => x.Id);
                    table.UniqueConstraint("AK_staff_onboarding_applications_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_onboarding_claim", "(\"ClaimId\" IS NULL AND \"ClaimVersion\" IS NULL) OR (\"ClaimId\" IS NOT NULL AND \"ClaimVersion\" > 0)");
                    table.CheckConstraint("CK_staff_onboarding_pending_profile", "\"Status\" IN (5, 7, 8) OR (\"VerifiedAccountEmail\" IS NOT NULL AND \"DisplayName\" IS NOT NULL)");
                    table.CheckConstraint("CK_staff_onboarding_source", "\"SourceKind\" IN (1, 2)");
                    table.CheckConstraint("CK_staff_onboarding_staff", "\"Status\" NOT IN (4, 5) OR \"StaffMemberId\" IS NOT NULL");
                    table.CheckConstraint("CK_staff_onboarding_status", "\"Status\" BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_staff_onboarding_terminal_redaction", "\"Status\" NOT IN (5, 7, 8) OR (\"VerifiedAccountEmail\" IS NULL AND \"DisplayName\" IS NULL AND \"LegalName\" IS NULL AND \"WorkEmail\" IS NULL AND \"WorkPhone\" IS NULL AND \"EmployeeNumber\" IS NULL AND \"JobTitle\" IS NULL AND \"Department\" IS NULL)");
                    table.CheckConstraint("CK_staff_onboarding_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "workspaces",
                table: "inbox_messages",
                columns: new[] { "Handler", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Status_ProcessedAtUtc",
                schema: "workspaces",
                table: "inbox_messages",
                columns: new[] { "Status", "ProcessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_applications_ScopeId_ClaimId",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                columns: new[] { "ScopeId", "ClaimId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_applications_ScopeId_SourceKind_SourceId_S~",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                columns: new[] { "ScopeId", "SourceKind", "SourceId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_applications_ScopeId_Status_CreatedAtUtc_Id",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                columns: new[] { "ScopeId", "Status", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "staff_onboarding_applications",
                schema: "workspaces");
        }
    }
}
