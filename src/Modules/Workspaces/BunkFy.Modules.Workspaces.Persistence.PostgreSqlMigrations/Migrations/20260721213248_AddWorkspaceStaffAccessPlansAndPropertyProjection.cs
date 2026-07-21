using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceStaffAccessPlansAndPropertyProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projection_rebuild_checkpoints",
                schema: "workspaces",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ProcessedCount = table.Column<long>(type: "bigint", nullable: false),
                    WrittenCount = table.Column<long>(type: "bigint", nullable: false),
                    SkippedCount = table.Column<long>(type: "bigint", nullable: false),
                    FailedCount = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionVersion = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projection_rebuild_checkpoints", x => new { x.ScopeId, x.ProjectionName, x.RunId });
                });

            migrationBuilder.CreateTable(
                name: "property_projection",
                schema: "workspaces",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_projection", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_workspaces_property_projection_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "staff_access_plans",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedBySubjectId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_access_plans", x => x.Id);
                    table.UniqueConstraint("AK_staff_access_plans_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_access_plans_source", "\"SourceKind\" IN (1, 2)");
                    table.CheckConstraint("CK_staff_access_plans_status", "\"Status\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_staff_access_plans_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "staff_access_plan_properties",
                schema: "workspaces",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_access_plan_properties", x => new { x.ScopeId, x.PlanId, x.PropertyId });
                    table.ForeignKey(
                        name: "FK_staff_access_plan_properties_staff_access_plans_ScopeId_Pla~",
                        columns: x => new { x.ScopeId, x.PlanId },
                        principalSchema: "workspaces",
                        principalTable: "staff_access_plans",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_property_projection_ScopeId_Status_Id",
                schema: "workspaces",
                table: "property_projection",
                columns: new[] { "ScopeId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_access_plan_properties_ScopeId_PropertyId_PlanId",
                schema: "workspaces",
                table: "staff_access_plan_properties",
                columns: new[] { "ScopeId", "PropertyId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_access_plans_ScopeId_Status_CreatedAtUtc_Id",
                schema: "workspaces",
                table: "staff_access_plans",
                columns: new[] { "ScopeId", "Status", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "projection_rebuild_checkpoints",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "property_projection",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "staff_access_plan_properties",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "staff_access_plans",
                schema: "workspaces");
        }
    }
}
