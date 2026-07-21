using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceStaffAccessLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_access_processes",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TargetState = table.Column<int>(type: "integer", nullable: false),
                    TargetStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_access_processes", x => x.Id);
                    table.UniqueConstraint("AK_staff_access_processes_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_access_process_staff_version", "\"TargetStaffVersion\" >= 2");
                    table.CheckConstraint("CK_staff_access_process_state", "\"State\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_staff_access_process_target", "\"TargetState\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_staff_access_process_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "staff_access_profile_snapshots",
                schema: "workspaces",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_access_profile_snapshots", x => new { x.ProcessId, x.ProfileId });
                    table.ForeignKey(
                        name: "FK_staff_access_profile_snapshots_staff_access_processes_Proce~",
                        column: x => x.ProcessId,
                        principalSchema: "workspaces",
                        principalTable: "staff_access_processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_access_processes_ScopeId_StaffMemberId_State_CreatedA~",
                schema: "workspaces",
                table: "staff_access_processes",
                columns: new[] { "ScopeId", "StaffMemberId", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_access_processes_ScopeId_StaffMemberId_TargetStaffVer~",
                schema: "workspaces",
                table: "staff_access_processes",
                columns: new[] { "ScopeId", "StaffMemberId", "TargetStaffVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_access_profile_snapshots",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "staff_access_processes",
                schema: "workspaces");
        }
    }
}
