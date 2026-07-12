using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ingestion");

            migrationBuilder.CreateTable(
                name: "adapter_connections",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdapterType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExecutionMode = table.Column<int>(type: "integer", nullable: false),
                    ConflictPolicy = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationReference = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SecretReference = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Checkpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adapter_connections", x => x.Id);
                    table.UniqueConstraint("AK_adapter_connections_ScopeId_Id", x => new { x.ScopeId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "ingestion",
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
                schema: "ingestion",
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
                name: "runs",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskAttempt = table.Column<int>(type: "integer", nullable: false),
                    StartingCheckpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AcceptedCheckpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ObservedCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.Id);
                    table.UniqueConstraint("AK_runs_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.ForeignKey(
                        name: "FK_runs_adapter_connections_ScopeId_ConnectionId",
                        columns: x => new { x.ScopeId, x.ConnectionId },
                        principalSchema: "ingestion",
                        principalTable: "adapter_connections",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "observation_receipts",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRecordType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceRevision = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RawPayloadFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_observation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.ForeignKey(
                        name: "FK_observation_receipts_adapter_connections_ScopeId_Connection~",
                        columns: x => new { x.ScopeId, x.ConnectionId },
                        principalSchema: "ingestion",
                        principalTable: "adapter_connections",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_observation_receipts_runs_ScopeId_RunId",
                        columns: x => new { x.ScopeId, x.RunId },
                        principalSchema: "ingestion",
                        principalTable: "runs",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "change_proposals",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedSnapshotFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseReservationDetailsRevision = table.Column<long>(type: "bigint", nullable: false),
                    Diff = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    DecisionActor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProductOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_proposals", x => x.Id);
                    table.UniqueConstraint("AK_change_proposals_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.ForeignKey(
                        name: "FK_change_proposals_adapter_connections_ScopeId_ConnectionId",
                        columns: x => new { x.ScopeId, x.ConnectionId },
                        principalSchema: "ingestion",
                        principalTable: "adapter_connections",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_change_proposals_observation_receipts_ScopeId_ReceiptId",
                        columns: x => new { x.ScopeId, x.ReceiptId },
                        principalSchema: "ingestion",
                        principalTable: "observation_receipts",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adapter_connections_ScopeId_AdapterType",
                schema: "ingestion",
                table: "adapter_connections",
                columns: new[] { "ScopeId", "AdapterType" });

            migrationBuilder.CreateIndex(
                name: "IX_adapter_connections_ScopeId_PropertyId_State",
                schema: "ingestion",
                table: "adapter_connections",
                columns: new[] { "ScopeId", "PropertyId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_change_proposals_ScopeId_ConnectionId",
                schema: "ingestion",
                table: "change_proposals",
                columns: new[] { "ScopeId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_change_proposals_ScopeId_PropertyId_State_CreatedAtUtc",
                schema: "ingestion",
                table: "change_proposals",
                columns: new[] { "ScopeId", "PropertyId", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_change_proposals_ScopeId_ReceiptId",
                schema: "ingestion",
                table: "change_proposals",
                columns: new[] { "ScopeId", "ReceiptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_change_proposals_ScopeId_ReservationId_CreatedAtUtc",
                schema: "ingestion",
                table: "change_proposals",
                columns: new[] { "ScopeId", "ReservationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_Handler_Status",
                schema: "ingestion",
                table: "inbox_messages",
                columns: new[] { "Handler", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_observation_receipts_ScopeId_ConnectionId_DeduplicationKey",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "ConnectionId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_observation_receipts_ScopeId_ConnectionId_OperationId",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "ConnectionId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_observation_receipts_ScopeId_ConnectionId_State_ReceivedAtU~",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "ConnectionId", "State", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_observation_receipts_ScopeId_RunId",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntil~",
                schema: "ingestion",
                table: "outbox_messages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_runs_ScopeId_ConnectionId_StartedAtUtc",
                schema: "ingestion",
                table: "runs",
                columns: new[] { "ScopeId", "ConnectionId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_runs_ScopeId_TaskRunId_TaskAttempt",
                schema: "ingestion",
                table: "runs",
                columns: new[] { "ScopeId", "TaskRunId", "TaskAttempt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "change_proposals",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "observation_receipts",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "runs",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "adapter_connections",
                schema: "ingestion");
        }
    }
}
