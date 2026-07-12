using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NormalizedSnapshotFileId",
                schema: "ingestion",
                table: "change_proposals",
                newName: "SourcePayloadFileId");

            migrationBuilder.CreateTable(
                name: "reservation_source_links",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    LastObservedReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastObservedSourceRevision = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastObservedSourceSequence = table.Column<long>(type: "bigint", nullable: true),
                    LastObservedSourceUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastObservedContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    LastAppliedReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastAppliedSourceRevision = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastAppliedSourceSequence = table.Column<long>(type: "bigint", nullable: true),
                    LastAppliedReservationDetailsRevision = table.Column<long>(type: "bigint", nullable: true),
                    LastAppliedNormalizedSnapshot = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: true),
                    LastProductOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActiveProductOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeferredReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_source_links", x => x.Id);
                    table.UniqueConstraint("AK_reservation_source_links_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.ForeignKey(
                        name: "FK_reservation_source_links_adapter_connections_ScopeId_Connec~",
                        columns: x => new { x.ScopeId, x.ConnectionId },
                        principalSchema: "ingestion",
                        principalTable: "adapter_connections",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reservation_dispatches",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerKind = table.Column<int>(type: "integer", nullable: false),
                    TriggerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SourceRevision = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SourceSequence = table.Column<long>(type: "bigint", nullable: true),
                    NormalizedSnapshot = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: false),
                    ExpectedDetailsRevision = table.Column<long>(type: "bigint", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ResultDetailsRevision = table.Column<long>(type: "bigint", nullable: true),
                    ResultReservationVersion = table.Column<long>(type: "bigint", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_dispatches", x => x.Id);
                    table.UniqueConstraint("AK_reservation_dispatches_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.ForeignKey(
                        name: "FK_reservation_dispatches_observation_receipts_ScopeId_Receipt~",
                        columns: x => new { x.ScopeId, x.ReceiptId },
                        principalSchema: "ingestion",
                        principalTable: "observation_receipts",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservation_dispatches_reservation_source_links_ScopeId_Sou~",
                        columns: x => new { x.ScopeId, x.SourceLinkId },
                        principalSchema: "ingestion",
                        principalTable: "reservation_source_links",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_dispatches_ScopeId_ReceiptId_CreatedAtUtc",
                schema: "ingestion",
                table: "reservation_dispatches",
                columns: new[] { "ScopeId", "ReceiptId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_dispatches_ScopeId_ReservationId_Kind_State",
                schema: "ingestion",
                table: "reservation_dispatches",
                columns: new[] { "ScopeId", "ReservationId", "Kind", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_dispatches_ScopeId_SourceLinkId_CreatedAtUtc",
                schema: "ingestion",
                table: "reservation_dispatches",
                columns: new[] { "ScopeId", "SourceLinkId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_dispatches_ScopeId_TriggerKind_TriggerId",
                schema: "ingestion",
                table: "reservation_dispatches",
                columns: new[] { "ScopeId", "TriggerKind", "TriggerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_source_links_ScopeId_ConnectionId_SourceSystem_~",
                schema: "ingestion",
                table: "reservation_source_links",
                columns: new[] { "ScopeId", "ConnectionId", "SourceSystem", "SourceReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_source_links_ScopeId_ConnectionId_State_Updated~",
                schema: "ingestion",
                table: "reservation_source_links",
                columns: new[] { "ScopeId", "ConnectionId", "State", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_source_links_ScopeId_ReservationId",
                schema: "ingestion",
                table: "reservation_source_links",
                columns: new[] { "ScopeId", "ReservationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_dispatches",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "reservation_source_links",
                schema: "ingestion");

            migrationBuilder.RenameColumn(
                name: "SourcePayloadFileId",
                schema: "ingestion",
                table: "change_proposals",
                newName: "NormalizedSnapshotFileId");
        }
    }
}
