using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialStaffProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "staff");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "staff",
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
                name: "outbox_messages",
                schema: "staff",
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
                name: "projection_rebuild_checkpoints",
                schema: "staff",
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
                schema: "staff",
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
                    table.CheckConstraint("CK_staff_property_projection_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "staff_members",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayNameSearch = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LegalNameSearch = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    WorkEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    WorkEmailSearch = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    WorkPhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WorkPhoneSearch = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EmployeeNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EmployeeNumberSearch = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    JobTitle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Department = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AuthSubjectId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionOrdinal = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SuspendedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DepartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DepartureEffectiveOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_members", x => x.Id);
                    table.UniqueConstraint("AK_staff_members_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_members_created_by", "length(trim(\"CreatedBy\")) > 0");
                    table.CheckConstraint("CK_staff_members_display_name", "length(trim(\"DisplayName\")) > 0");
                    table.CheckConstraint("CK_staff_members_last_changed_by", "length(trim(\"LastChangedBy\")) > 0");
                    table.CheckConstraint("CK_staff_members_lifecycle", "(\"Status\" = 1 AND \"SuspendedAtUtc\" IS NULL AND \"DepartedAtUtc\" IS NULL AND \"DepartureEffectiveOn\" IS NULL) OR (\"Status\" = 2 AND \"SuspendedAtUtc\" IS NOT NULL AND \"DepartedAtUtc\" IS NULL AND \"DepartureEffectiveOn\" IS NULL) OR (\"Status\" = 3 AND \"SuspendedAtUtc\" IS NULL AND \"DepartedAtUtc\" IS NOT NULL AND \"DepartureEffectiveOn\" IS NOT NULL)");
                    table.CheckConstraint("CK_staff_members_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "property_assignments",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyJobTitle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedAtVersion = table.Column<long>(type: "bigint", nullable: false),
                    UnassignedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UnassignmentReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UnassignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UnassignedAtVersion = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_assignments", x => new { x.ScopeId, x.StaffMemberId, x.Id });
                    table.CheckConstraint("CK_staff_assignments_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.CheckConstraint("CK_staff_assignments_lifecycle", "(\"IsCurrent\" AND \"EffectiveTo\" IS NULL AND \"UnassignedBy\" IS NULL AND \"UnassignedAtUtc\" IS NULL AND \"UnassignedAtVersion\" IS NULL) OR (NOT \"IsCurrent\" AND NOT \"IsPrimary\" AND \"EffectiveTo\" IS NOT NULL AND \"UnassignedBy\" IS NOT NULL AND \"UnassignedAtUtc\" IS NOT NULL AND \"UnassignedAtVersion\" IS NOT NULL)");
                    table.CheckConstraint("CK_staff_assignments_versions", "\"AssignedAtVersion\" >= 2 AND (\"UnassignedAtVersion\" IS NULL OR \"UnassignedAtVersion\" >= \"AssignedAtVersion\")");
                    table.ForeignKey(
                        name: "FK_property_assignments_staff_members_ScopeId_StaffMemberId",
                        columns: x => new { x.ScopeId, x.StaffMemberId },
                        principalSchema: "staff",
                        principalTable: "staff_members",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "staff",
                table: "inbox_messages",
                columns: new[] { "Handler", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntil~",
                schema: "staff",
                table: "outbox_messages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_property_assignments_ScopeId_PropertyId_IsCurrent_StaffMemb~",
                schema: "staff",
                table: "property_assignments",
                columns: new[] { "ScopeId", "PropertyId", "IsCurrent", "StaffMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_property_assignments_ScopeId_StaffMemberId_PropertyId_IsCur~",
                schema: "staff",
                table: "property_assignments",
                columns: new[] { "ScopeId", "StaffMemberId", "PropertyId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_property_projection_ScopeId_Status_Id",
                schema: "staff",
                table: "property_projection",
                columns: new[] { "ScopeId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_members_ProjectionOrdinal",
                schema: "staff",
                table: "staff_members",
                column: "ProjectionOrdinal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_members_ScopeId_AuthSubjectId",
                schema: "staff",
                table: "staff_members",
                columns: new[] { "ScopeId", "AuthSubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_members_ScopeId_EmployeeNumberSearch",
                schema: "staff",
                table: "staff_members",
                columns: new[] { "ScopeId", "EmployeeNumberSearch" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_members_ScopeId_Status_DisplayNameSearch_Id",
                schema: "staff",
                table: "staff_members",
                columns: new[] { "ScopeId", "Status", "DisplayNameSearch", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "projection_rebuild_checkpoints",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "property_assignments",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "property_projection",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_members",
                schema: "staff");
        }
    }
}
