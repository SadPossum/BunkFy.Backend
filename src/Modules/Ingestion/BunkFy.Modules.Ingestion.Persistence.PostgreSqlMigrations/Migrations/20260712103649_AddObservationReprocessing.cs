using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddObservationReprocessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveReprocessingAttemptId",
                schema: "ingestion",
                table: "observation_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParserOutputIndex",
                schema: "ingestion",
                table: "observation_receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParserType",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParserVersion",
                schema: "ingestion",
                table: "observation_receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReprocessingAttemptId",
                schema: "ingestion",
                table: "observation_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReprocessingReservationExpiresAtUtc",
                schema: "ingestion",
                table: "observation_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceReceiptId",
                schema: "ingestion",
                table: "observation_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "observation_reprocessing_attempts",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParserType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParserVersion = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    LastTaskAttempt = table.Column<int>(type: "integer", nullable: false),
                    ParsedCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    DuplicateCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false),
                    LastErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReservationExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation_reprocessing_attempts", x => x.Id);
                    table.UniqueConstraint("AK_observation_reprocessing_attempts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.ForeignKey(
                        name: "FK_observation_reprocessing_attempts_adapter_connections_Scope~",
                        columns: x => new { x.ScopeId, x.ConnectionId },
                        principalSchema: "ingestion",
                        principalTable: "adapter_connections",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_observation_reprocessing_attempts_observation_receipts_Scop~",
                        columns: x => new { x.ScopeId, x.SourceReceiptId },
                        principalSchema: "ingestion",
                        principalTable: "observation_receipts",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "observation_reprocessing_outputs",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputIndex = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceRevision = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation_reprocessing_outputs", x => x.Id);
                    table.UniqueConstraint("AK_observation_reprocessing_outputs_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.ForeignKey(
                        name: "FK_observation_reprocessing_outputs_observation_receipts_Scope~",
                        columns: x => new { x.ScopeId, x.ReceiptId },
                        principalSchema: "ingestion",
                        principalTable: "observation_receipts",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_observation_reprocessing_outputs_observation_reprocessing_a~",
                        columns: x => new { x.ScopeId, x.AttemptId },
                        principalSchema: "ingestion",
                        principalTable: "observation_reprocessing_attempts",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_observation_receipts_ScopeId_ActiveReprocessingAttemptId_Re~",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "ActiveReprocessingAttemptId", "ReprocessingReservationExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_observation_receipts_ScopeId_ReprocessingAttemptId",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "ReprocessingAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_observation_receipts_ScopeId_SourceReceiptId_ReceivedAtUtc",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "SourceReceiptId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_observation_reprocessing_attempts_ScopeId_ConnectionId",
                schema: "ingestion",
                table: "observation_reprocessing_attempts",
                columns: new[] { "ScopeId", "ConnectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_observation_reprocessing_attempts_ScopeId_SourceReceiptId_S~",
                schema: "ingestion",
                table: "observation_reprocessing_attempts",
                columns: new[] { "ScopeId", "SourceReceiptId", "State", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_observation_reprocessing_attempts_ScopeId_TaskRunId",
                schema: "ingestion",
                table: "observation_reprocessing_attempts",
                columns: new[] { "ScopeId", "TaskRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_observation_reprocessing_outputs_ScopeId_AttemptId_OutputIn~",
                schema: "ingestion",
                table: "observation_reprocessing_outputs",
                columns: new[] { "ScopeId", "AttemptId", "OutputIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_observation_reprocessing_outputs_ScopeId_ReceiptId",
                schema: "ingestion",
                table: "observation_reprocessing_outputs",
                columns: new[] { "ScopeId", "ReceiptId" });

            migrationBuilder.AddForeignKey(
                name: "FK_observation_receipts_observation_receipts_ScopeId_SourceRec~",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "SourceReceiptId" },
                principalSchema: "ingestion",
                principalTable: "observation_receipts",
                principalColumns: new[] { "ScopeId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_observation_receipts_observation_reprocessing_attempts_Scop~",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "ReprocessingAttemptId" },
                principalSchema: "ingestion",
                principalTable: "observation_reprocessing_attempts",
                principalColumns: new[] { "ScopeId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_observation_receipts_observation_receipts_ScopeId_SourceRec~",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_observation_receipts_observation_reprocessing_attempts_Scop~",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropTable(
                name: "observation_reprocessing_outputs",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "observation_reprocessing_attempts",
                schema: "ingestion");

            migrationBuilder.DropIndex(
                name: "IX_observation_receipts_ScopeId_ActiveReprocessingAttemptId_Re~",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropIndex(
                name: "IX_observation_receipts_ScopeId_ReprocessingAttemptId",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropIndex(
                name: "IX_observation_receipts_ScopeId_SourceReceiptId_ReceivedAtUtc",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "ActiveReprocessingAttemptId",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "ParserOutputIndex",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "ParserType",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "ParserVersion",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "ReprocessingAttemptId",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "ReprocessingReservationExpiresAtUtc",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "SourceReceiptId",
                schema: "ingestion",
                table: "observation_receipts");
        }
    }
}
