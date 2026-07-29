using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationRecordRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_reservation_anonymisation_tombstones_contract",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TerminalAtUtc",
                schema: "reservations",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Authority",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE reservations.reservations
                SET "TerminalAtUtc" =
                    CASE "Status"
                        WHEN 8 THEN COALESCE("NoShowAtUtc", "UpdatedAtUtc", "CreatedAtUtc")
                        WHEN 10 THEN COALESCE("CheckedOutAtUtc", "UpdatedAtUtc", "CreatedAtUtc")
                        ELSE COALESCE("UpdatedAtUtc", "CreatedAtUtc")
                    END
                WHERE "Status" IN (3, 5, 8, 10)
                  AND "TerminalAtUtc" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE reservations.reservation_anonymisation_tombstones
                SET "Authority" = 1,
                    "ContractVersion" = 2;
                """);

            migrationBuilder.CreateTable(
                name: "reservation_retention_executions",
                schema: "reservations",
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
                    table.PrimaryKey("PK_reservation_retention_executions", x => x.Id);
                    table.UniqueConstraint("AK_reservation_retention_executions_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_retention_executions_counts", "\"AffectedCount\" >= 0 AND (\"ScannedCount\" IS NULL OR \"ScannedCount\" >= 0) AND (\"RemainingCount\" IS NULL OR \"RemainingCount\" >= 0) AND (\"ScannedCount\" IS NULL OR \"AffectedCount\" <= \"ScannedCount\")");
                    table.CheckConstraint("CK_reservation_retention_executions_cursor", "\"StartingProjectionOrdinal\" >= 0");
                    table.CheckConstraint("CK_reservation_retention_executions_key", "\"DataClassKey\" ~ '^[A-Za-z0-9.-]+$'");
                    table.CheckConstraint("CK_reservation_retention_executions_policy", "\"ExecutionPolicyVersion\" >= 1 AND \"Attempt\" >= 1 AND \"DeadlineUtc\" > \"StartedAtUtc\"");
                    table.CheckConstraint("CK_reservation_retention_executions_state", "(\"State\" = 1 AND \"CompletedAtUtc\" IS NULL AND \"ScannedCount\" IS NULL AND \"RemainingCount\" IS NULL AND \"OutcomeCode\" IS NULL AND \"HoldReviewDueAtUtc\" IS NULL) OR (\"State\" IN (2, 3, 4) AND \"CompletedAtUtc\" BETWEEN \"StartedAtUtc\" AND \"DeadlineUtc\" AND \"ScannedCount\" IS NOT NULL AND \"RemainingCount\" IS NOT NULL AND \"OutcomeCode\" ~ '^[A-Za-z0-9.-]+$' AND ((\"State\" = 3 AND \"HoldReviewDueAtUtc\" IS NOT NULL) OR (\"State\" <> 3 AND \"HoldReviewDueAtUtc\" IS NULL)))");
                    table.CheckConstraint("CK_reservation_retention_executions_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "reservation_retention_sweep_checkpoints",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataClassKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExecutionPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    AfterProjectionOrdinal = table.Column<long>(type: "bigint", nullable: false),
                    LastExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_retention_sweep_checkpoints", x => x.Id);
                    table.UniqueConstraint("AK_reservation_retention_sweep_checkpoints_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_retention_checkpoints_cursor", "\"AfterProjectionOrdinal\" >= 0");
                    table.CheckConstraint("CK_reservation_retention_checkpoints_key", "\"DataClassKey\" ~ '^[A-Za-z0-9.-]+$'");
                    table.CheckConstraint("CK_reservation_retention_checkpoints_policy", "\"ExecutionPolicyVersion\" >= 1");
                    table.CheckConstraint("CK_reservation_retention_checkpoints_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "reservation_retention_anonymisation_receipts",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    SelectedDetailsRevision = table.Column<long>(type: "bigint", nullable: false),
                    ResultingDetailsRevision = table.Column<long>(type: "bigint", nullable: false),
                    TerminalAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetentionDeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RedactedHistoryCount = table.Column<int>(type: "integer", nullable: false),
                    RemovedGuestLinkCount = table.Column<int>(type: "integer", nullable: false),
                    ReducedExternalOperationCount = table.Column<int>(type: "integer", nullable: false),
                    SuppressedReminderCount = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_retention_anonymisation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_reservation_retention_anonymisation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_retention_receipts_actor", "\"ActorId\" = 'system:retention'");
                    table.CheckConstraint("CK_reservation_retention_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_reservation_retention_receipts_counts", "\"RedactedHistoryCount\" >= 1 AND \"RemovedGuestLinkCount\" >= 0 AND \"ReducedExternalOperationCount\" >= 0 AND \"SuppressedReminderCount\" >= 0");
                    table.CheckConstraint("CK_reservation_retention_receipts_deadline", "\"RetentionDeadlineUtc\" >= \"TerminalAtUtc\" AND \"CompletedAtUtc\" >= \"RetentionDeadlineUtc\"");
                    table.CheckConstraint("CK_reservation_retention_receipts_digests", "char_length(\"PolicyEvidenceSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_reservation_retention_receipts_versions", "\"SelectedReservationVersion\" >= 1 AND \"ResultingReservationVersion\" = \"SelectedReservationVersion\" + 1 AND \"SelectedDetailsRevision\" >= 1 AND \"ResultingDetailsRevision\" = \"SelectedDetailsRevision\" + 1");
                    table.ForeignKey(
                        name: "FK_reservation_retention_anonymisation_receipts_reservation_re~",
                        columns: x => new { x.ScopeId, x.ExecutionId },
                        principalSchema: "reservations",
                        principalTable: "reservation_retention_executions",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservation_retention_anonymisation_receipts_reservations_S~",
                        columns: x => new { x.ScopeId, x.ReservationId },
                        principalSchema: "reservations",
                        principalTable: "reservations",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_ScopeId_ProjectionOrdinal_TerminalAtUtc_IsAnon~",
                schema: "reservations",
                table: "reservations",
                columns: new[] { "ScopeId", "ProjectionOrdinal", "TerminalAtUtc", "IsAnonymised" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_terminal_time",
                schema: "reservations",
                table: "reservations",
                sql: "(\"Status\" IN (3, 5, 8, 10) AND \"TerminalAtUtc\" IS NOT NULL) OR (\"Status\" NOT IN (3, 5, 8, 10) AND \"TerminalAtUtc\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservation_anonymisation_tombstones_authority",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones",
                sql: "\"Authority\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservation_anonymisation_tombstones_contract",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones",
                sql: "\"ContractVersion\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_retention_anonymisation_receipts_ScopeId_Execut~",
                schema: "reservations",
                table: "reservation_retention_anonymisation_receipts",
                columns: new[] { "ScopeId", "ExecutionId", "ReservationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_retention_anonymisation_receipts_ScopeId_Reserv~",
                schema: "reservations",
                table: "reservation_retention_anonymisation_receipts",
                columns: new[] { "ScopeId", "ReservationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_retention_executions_ScopeId_DataClassKey_Compl~",
                schema: "reservations",
                table: "reservation_retention_executions",
                columns: new[] { "ScopeId", "DataClassKey", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_retention_sweep_checkpoints_ScopeId_DataClassKe~",
                schema: "reservations",
                table: "reservation_retention_sweep_checkpoints",
                columns: new[] { "ScopeId", "DataClassKey", "ExecutionPolicyVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_retention_anonymisation_receipts",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservation_retention_sweep_checkpoints",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservation_retention_executions",
                schema: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_ScopeId_ProjectionOrdinal_TerminalAtUtc_IsAnon~",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_terminal_time",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservation_anonymisation_tombstones_authority",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservation_anonymisation_tombstones_contract",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones");

            migrationBuilder.Sql(
                """
                UPDATE reservations.reservation_anonymisation_tombstones
                SET "ContractVersion" = 1;
                """);

            migrationBuilder.DropColumn(
                name: "TerminalAtUtc",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "Authority",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservation_anonymisation_tombstones_contract",
                schema: "reservations",
                table: "reservation_anonymisation_tombstones",
                sql: "\"ContractVersion\" = 1");
        }
    }
}
