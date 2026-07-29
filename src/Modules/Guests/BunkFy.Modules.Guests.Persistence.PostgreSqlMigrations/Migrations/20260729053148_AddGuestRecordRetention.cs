using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestRecordRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_guest_anonymisation_tombstones_contract",
                schema: "guests",
                table: "guest_anonymisation_tombstones");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                schema: "guests",
                table: "property_projection",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Authority",
                schema: "guests",
                table: "guest_anonymisation_tombstones",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE guests.guest_anonymisation_tombstones
                SET "Authority" = 1,
                    "ContractVersion" = 2
                WHERE "Authority" IS NULL
                  AND "ContractVersion" = 1;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM guests.guest_anonymisation_tombstones
                        WHERE "Authority" IS NULL
                           OR "Authority" NOT IN (1, 2)
                           OR "ContractVersion" <> 2
                    ) THEN
                        RAISE EXCEPTION 'Cannot upgrade Guests because anonymisation tombstone authority could not be classified.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Authority",
                schema: "guests",
                table: "guest_anonymisation_tombstones",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "guest_retention_executions",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataClassKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExecutionPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    StartingProjectionOrdinal = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AffectedCount = table.Column<int>(type: "integer", nullable: false),
                    ScannedCount = table.Column<int>(type: "integer", nullable: true),
                    RemainingCount = table.Column<int>(type: "integer", nullable: true),
                    OutcomeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HoldReviewDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_retention_executions", x => x.Id);
                    table.UniqueConstraint("AK_guest_retention_executions_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_retention_executions_counts", "\"AffectedCount\" >= 0 AND (\"ScannedCount\" IS NULL OR \"ScannedCount\" >= 0) AND (\"RemainingCount\" IS NULL OR \"RemainingCount\" >= 0) AND (\"ScannedCount\" IS NULL OR \"AffectedCount\" <= \"ScannedCount\")");
                    table.CheckConstraint("CK_guest_retention_executions_cursor", "\"StartingProjectionOrdinal\" >= 0");
                    table.CheckConstraint("CK_guest_retention_executions_key", "length(trim(\"DataClassKey\")) > 0");
                    table.CheckConstraint("CK_guest_retention_executions_policy", "\"ExecutionPolicyVersion\" >= 1 AND \"Attempt\" >= 1 AND \"DeadlineUtc\" > \"StartedAtUtc\"");
                    table.CheckConstraint("CK_guest_retention_executions_state", "(\"State\" = 1 AND \"CompletedAtUtc\" IS NULL AND \"ScannedCount\" IS NULL AND \"RemainingCount\" IS NULL AND \"OutcomeCode\" IS NULL AND \"HoldReviewDueAtUtc\" IS NULL) OR (\"State\" IN (2, 3, 4) AND \"CompletedAtUtc\" BETWEEN \"StartedAtUtc\" AND \"DeadlineUtc\" AND \"ScannedCount\" IS NOT NULL AND \"RemainingCount\" IS NOT NULL AND \"OutcomeCode\" ~ '^[A-Za-z0-9.-]+$' AND ((\"State\" = 3 AND \"HoldReviewDueAtUtc\" IS NOT NULL) OR (\"State\" <> 3 AND \"HoldReviewDueAtUtc\" IS NULL)))");
                    table.CheckConstraint("CK_guest_retention_executions_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "guest_retention_sweep_checkpoints",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataClassKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AfterProjectionOrdinal = table.Column<long>(type: "bigint", nullable: false),
                    LastExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_retention_sweep_checkpoints", x => x.Id);
                    table.UniqueConstraint("AK_guest_retention_sweep_checkpoints_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_retention_sweep_checkpoints_cursor", "\"AfterProjectionOrdinal\" >= 0");
                    table.CheckConstraint("CK_guest_retention_sweep_checkpoints_key", "length(trim(\"DataClassKey\")) > 0");
                    table.CheckConstraint("CK_guest_retention_sweep_checkpoints_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "guest_retention_anonymisation_receipts",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedGuestVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingGuestVersion = table.Column<long>(type: "bigint", nullable: false),
                    AffectedPropertyCount = table.Column<int>(type: "integer", nullable: false),
                    RetentionDeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PolicySetSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_retention_anonymisation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_guest_retention_anonymisation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_retention_receipts_actor", "length(trim(\"ActorId\")) > 0");
                    table.CheckConstraint("CK_guest_retention_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_guest_retention_receipts_deadline", "\"CompletedAtUtc\" >= \"RetentionDeadlineUtc\"");
                    table.CheckConstraint("CK_guest_retention_receipts_digests", "\"PolicySetSha256\" ~ '^[0-9a-f]{64}$' AND \"CanonicalSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_guest_retention_receipts_properties", "\"AffectedPropertyCount\" BETWEEN 1 AND 256");
                    table.CheckConstraint("CK_guest_retention_receipts_versions", "\"SelectedGuestVersion\" >= 1 AND \"ResultingGuestVersion\" = \"SelectedGuestVersion\" + 1");
                    table.ForeignKey(
                        name: "FK_guest_retention_anonymisation_receipts_guest_profiles_Scope~",
                        columns: x => new { x.ScopeId, x.GuestId },
                        principalSchema: "guests",
                        principalTable: "guest_profiles",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guest_retention_anonymisation_receipts_guest_retention_exec~",
                        columns: x => new { x.ScopeId, x.ExecutionId },
                        principalSchema: "guests",
                        principalTable: "guest_retention_executions",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guest_profiles_ScopeId_Status_ProjectionOrdinal",
                schema: "guests",
                table: "guest_profiles",
                columns: new[] { "ScopeId", "Status", "ProjectionOrdinal" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_guest_anonymisation_tombstones_authority",
                schema: "guests",
                table: "guest_anonymisation_tombstones",
                sql: "\"Authority\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_guest_anonymisation_tombstones_contract",
                schema: "guests",
                table: "guest_anonymisation_tombstones",
                sql: "\"ContractVersion\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_guest_retention_anonymisation_receipts_ScopeId_ExecutionId_~",
                schema: "guests",
                table: "guest_retention_anonymisation_receipts",
                columns: new[] { "ScopeId", "ExecutionId", "GuestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_retention_anonymisation_receipts_ScopeId_GuestId",
                schema: "guests",
                table: "guest_retention_anonymisation_receipts",
                columns: new[] { "ScopeId", "GuestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_retention_executions_ScopeId_DataClassKey_CompletedAt~",
                schema: "guests",
                table: "guest_retention_executions",
                columns: new[] { "ScopeId", "DataClassKey", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_guest_retention_sweep_checkpoints_ScopeId_DataClassKey",
                schema: "guests",
                table: "guest_retention_sweep_checkpoints",
                columns: new[] { "ScopeId", "DataClassKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM guests.guest_anonymisation_tombstones
                        WHERE "Authority" <> 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade Guests while retention anonymisation tombstones exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "guest_retention_anonymisation_receipts",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "guest_retention_sweep_checkpoints",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "guest_retention_executions",
                schema: "guests");

            migrationBuilder.DropIndex(
                name: "IX_guest_profiles_ScopeId_Status_ProjectionOrdinal",
                schema: "guests",
                table: "guest_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_guest_anonymisation_tombstones_authority",
                schema: "guests",
                table: "guest_anonymisation_tombstones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_guest_anonymisation_tombstones_contract",
                schema: "guests",
                table: "guest_anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.Sql(
                """
                UPDATE guests.guest_anonymisation_tombstones
                SET "ContractVersion" = 1
                WHERE "Authority" = 1;
                """);

            migrationBuilder.DropColumn(
                name: "Authority",
                schema: "guests",
                table: "guest_anonymisation_tombstones");

            migrationBuilder.AddCheckConstraint(
                name: "CK_guest_anonymisation_tombstones_contract",
                schema: "guests",
                table: "guest_anonymisation_tombstones",
                sql: "\"ContractVersion\" = 1");
        }
    }
}
