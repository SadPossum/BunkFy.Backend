using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Retention.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "retention");

            migrationBuilder.CreateTable(
                name: "executions",
                schema: "retention",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DataClassKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetKind = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutionPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScannedCount = table.Column<int>(type: "integer", nullable: true),
                    AffectedCount = table.Column<int>(type: "integer", nullable: true),
                    RemainingCount = table.Column<int>(type: "integer", nullable: true),
                    OutcomeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HoldReviewDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executions", x => x.Id);
                    table.UniqueConstraint("AK_executions_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_retention_executions_coordinate", "(\"TargetKind\" = 1 AND \"PropertyId\" IS NULL) OR (\"TargetKind\" = 2 AND \"PropertyId\" IS NOT NULL)");
                    table.CheckConstraint("CK_retention_executions_state", "(\"State\" = 1 AND \"CompletedAtUtc\" IS NULL AND \"ScannedCount\" IS NULL AND \"AffectedCount\" IS NULL AND \"RemainingCount\" IS NULL AND \"OutcomeCode\" IS NULL AND \"HoldReviewDueAtUtc\" IS NULL) OR (\"State\" IN (2, 3, 4) AND \"CompletedAtUtc\" IS NOT NULL AND \"ScannedCount\" >= 0 AND \"AffectedCount\" BETWEEN 0 AND \"ScannedCount\" AND \"RemainingCount\" >= 0 AND length(trim(\"OutcomeCode\")) > 0 AND ((\"State\" = 3 AND \"HoldReviewDueAtUtc\" IS NOT NULL) OR (\"State\" <> 3 AND \"HoldReviewDueAtUtc\" IS NULL)))");
                    table.CheckConstraint("CK_retention_executions_time", "\"DeadlineUtc\" > \"StartedAtUtc\"");
                    table.CheckConstraint("CK_retention_executions_versions", "\"ExecutionPolicyVersion\" >= 1 AND \"Attempt\" >= 1 AND \"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "retention",
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
                name: "property_projection",
                schema: "retention",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsKnown = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsProcessingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RetentionPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    TopologySourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    PolicySourceVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_projection", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_retention_property_projection_policy", "(\"PolicySourceVersion\" = 0 AND \"RetentionPolicyVersion\" IS NULL AND \"IsProcessingEnabled\" = FALSE) OR (\"PolicySourceVersion\" >= 1 AND \"RetentionPolicyVersion\" >= 1)");
                    table.CheckConstraint("CK_retention_property_projection_versions", "\"TopologySourceVersion\" >= 0 AND \"PolicySourceVersion\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "schedule_state",
                schema: "retention",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DataClassKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExecutionPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    LastStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    LastScannedCount = table.Column<int>(type: "integer", nullable: true),
                    LastAffectedCount = table.Column<int>(type: "integer", nullable: true),
                    LastRemainingCount = table.Column<int>(type: "integer", nullable: true),
                    OutcomeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HoldReviewDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_state", x => new { x.ScopeId, x.OwnerKey, x.DataClassKey, x.TargetKey, x.ExecutionPolicyVersion });
                    table.CheckConstraint("CK_retention_schedule_state_result", "\"State\" IN (1, 2, 3, 4) AND ((\"LastCompletedAtUtc\" IS NULL AND \"LastScannedCount\" IS NULL AND \"LastAffectedCount\" IS NULL AND \"LastRemainingCount\" IS NULL AND \"OutcomeCode\" IS NULL) OR (\"LastCompletedAtUtc\" IS NOT NULL AND \"LastScannedCount\" >= 0 AND \"LastAffectedCount\" BETWEEN 0 AND \"LastScannedCount\" AND \"LastRemainingCount\" >= 0 AND length(trim(\"OutcomeCode\")) > 0))");
                    table.CheckConstraint("CK_retention_schedule_state_target", "(\"TargetKey\" = 'tenant' AND \"PropertyId\" IS NULL) OR (char_length(\"TargetKey\") = 32 AND \"PropertyId\" IS NOT NULL)");
                    table.CheckConstraint("CK_retention_schedule_state_time", "\"NextDueAtUtc\" > \"LastStartedAtUtc\" AND (\"LastCompletedAtUtc\" IS NULL OR \"LastCompletedAtUtc\" >= \"LastStartedAtUtc\")");
                    table.CheckConstraint("CK_retention_schedule_state_versions", "\"ExecutionPolicyVersion\" >= 1 AND \"Version\" >= 1 AND \"ConsecutiveFailures\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "tenant_projection",
                schema: "retention",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_projection", x => x.ScopeId);
                    table.CheckConstraint("CK_retention_tenant_projection_version", "\"SourceVersion\" >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_executions_ScopeId_OwnerKey_DataClassKey_PropertyId_Executi~",
                schema: "retention",
                table: "executions",
                columns: new[] { "ScopeId", "OwnerKey", "DataClassKey", "PropertyId", "ExecutionPolicyVersion", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_executions_ScopeId_State_CompletedAtUtc",
                schema: "retention",
                table: "executions",
                columns: new[] { "ScopeId", "State", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "retention",
                table: "inbox_messages",
                columns: new[] { "Handler", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Status_ProcessedAtUtc",
                schema: "retention",
                table: "inbox_messages",
                columns: new[] { "Status", "ProcessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_property_projection_ScopeId_IsActive_IsProcessingEnabled_Id",
                schema: "retention",
                table: "property_projection",
                columns: new[] { "ScopeId", "IsActive", "IsProcessingEnabled", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_state_ScopeId_HoldReviewDueAtUtc",
                schema: "retention",
                table: "schedule_state",
                columns: new[] { "ScopeId", "HoldReviewDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_state_ScopeId_State_NextDueAtUtc",
                schema: "retention",
                table: "schedule_state",
                columns: new[] { "ScopeId", "State", "NextDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_projection_IsActive_ScopeId",
                schema: "retention",
                table: "tenant_projection",
                columns: new[] { "IsActive", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_projection_OrganizationId",
                schema: "retention",
                table: "tenant_projection",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "executions",
                schema: "retention");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "retention");

            migrationBuilder.DropTable(
                name: "property_projection",
                schema: "retention");

            migrationBuilder.DropTable(
                name: "schedule_state",
                schema: "retention");

            migrationBuilder.DropTable(
                name: "tenant_projection",
                schema: "retention");
        }
    }
}
