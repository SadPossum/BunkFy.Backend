using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionAnonymisationRestore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "reservation_source_links",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "reservation_dispatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "observation_reprocessing_outputs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "ingestion",
                table: "observation_reprocessing_outputs",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "observation_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "change_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "anonymisation_tombstones",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedSourceLinkVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingSourceLinkVersion = table.Column<long>(type: "bigint", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OriginallyCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantSequence = table.Column<long>(type: "bigint", nullable: false),
                    LedgerEntrySha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    GraphRecordCount = table.Column<int>(type: "integer", nullable: false),
                    FingerprintCount = table.Column<int>(type: "integer", nullable: false),
                    RawPayloadCount = table.Column<int>(type: "integer", nullable: false),
                    ReplayStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anonymisation_tombstones", x => x.Id);
                    table.UniqueConstraint("AK_anonymisation_tombstones_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_ingestion_anonymisation_tombstones_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_ingestion_anonymisation_tombstones_counts", "\"GraphRecordCount\" >= 1 AND \"FingerprintCount\" >= 1 AND \"RawPayloadCount\" >= 0");
                    table.CheckConstraint("CK_ingestion_anonymisation_tombstones_revision", "\"Revision\" >= 1");
                    table.CheckConstraint("CK_ingestion_anonymisation_tombstones_state", "\"State\" IN (1, 2)");
                    table.CheckConstraint("CK_ingestion_anonymisation_tombstones_versions", "\"SelectedSourceLinkVersion\" >= 1 AND \"ResultingSourceLinkVersion\" = \"SelectedSourceLinkVersion\" + 1");
                });

            migrationBuilder.CreateTable(
                name: "source_operation_locks",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_operation_locks", x => x.Id);
                    table.UniqueConstraint("AK_source_operation_locks_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_ingestion_source_operation_locks_revision", "\"Revision\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "anonymisation_fingerprints",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TombstoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    KeyVersion = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anonymisation_fingerprints", x => x.Id);
                    table.UniqueConstraint("AK_anonymisation_fingerprints_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_ingestion_anonymisation_fingerprints_key_version", "\"KeyVersion\" >= 1");
                    table.CheckConstraint("CK_ingestion_anonymisation_fingerprints_purpose", "\"Purpose\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_anonymisation_fingerprints_anonymisation_tombstones_ScopeId~",
                        columns: x => new { x.ScopeId, x.TombstoneId },
                        principalSchema: "ingestion",
                        principalTable: "anonymisation_tombstones",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "anonymisation_record_plan",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TombstoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    ReductionVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingVersion = table.Column<long>(type: "bigint", nullable: false),
                    RawPayloadFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawPayloadConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anonymisation_record_plan", x => x.Id);
                    table.UniqueConstraint("AK_anonymisation_record_plan_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_ingestion_anonymisation_record_plan_kind", "\"Kind\" IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_ingestion_anonymisation_record_plan_raw_payload", "(\"RawPayloadFileId\" IS NULL AND \"RawPayloadConnectionId\" IS NULL) OR (\"Kind\" = 2 AND \"RawPayloadFileId\" IS NOT NULL AND \"RawPayloadConnectionId\" IS NOT NULL)");
                    table.CheckConstraint("CK_ingestion_anonymisation_record_plan_versions", "\"SelectedVersion\" >= 1 AND \"ReductionVersion\" = \"SelectedVersion\" + CASE WHEN \"Kind\" = 5 THEN 0 ELSE 1 END AND \"ResultingVersion\" = \"ReductionVersion\" + CASE WHEN \"RawPayloadFileId\" IS NULL THEN 0 ELSE 1 END");
                    table.ForeignKey(
                        name: "FK_anonymisation_record_plan_anonymisation_tombstones_ScopeId_~",
                        columns: x => new { x.ScopeId, x.TombstoneId },
                        principalSchema: "ingestion",
                        principalTable: "anonymisation_tombstones",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_observation_reprocessing_outputs_version",
                schema: "ingestion",
                table: "observation_reprocessing_outputs",
                sql: "\"Version\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_fingerprints_ScopeId_Purpose_KeyVersion_Sha256",
                schema: "ingestion",
                table: "anonymisation_fingerprints",
                columns: new[] { "ScopeId", "Purpose", "KeyVersion", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_fingerprints_ScopeId_TombstoneId",
                schema: "ingestion",
                table: "anonymisation_fingerprints",
                columns: new[] { "ScopeId", "TombstoneId" });

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_record_plan_ScopeId_TombstoneId_Kind_RecordId",
                schema: "ingestion",
                table: "anonymisation_record_plan",
                columns: new[] { "ScopeId", "TombstoneId", "Kind", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_record_plan_ScopeId_TombstoneId_RawPayloadFil~",
                schema: "ingestion",
                table: "anonymisation_record_plan",
                columns: new[] { "ScopeId", "TombstoneId", "RawPayloadFileId" });

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_tombstones_ScopeId_ConnectionId_State",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                columns: new[] { "ScopeId", "ConnectionId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_tombstones_ScopeId_LedgerEntryId",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                columns: new[] { "ScopeId", "LedgerEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_tombstones_ScopeId_OwnerReceiptId",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                columns: new[] { "ScopeId", "OwnerReceiptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_tombstones_State",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_source_operation_locks_ScopeId_SourceLinkId",
                schema: "ingestion",
                table: "source_operation_locks",
                columns: new[] { "ScopeId", "SourceLinkId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anonymisation_fingerprints",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "anonymisation_record_plan",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "source_operation_locks",
                schema: "ingestion");

            migrationBuilder.DropTable(
                name: "anonymisation_tombstones",
                schema: "ingestion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_observation_reprocessing_outputs_version",
                schema: "ingestion",
                table: "observation_reprocessing_outputs");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "reservation_source_links");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "reservation_dispatches");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "observation_reprocessing_outputs");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "ingestion",
                table: "observation_reprocessing_outputs");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                schema: "ingestion",
                table: "change_proposals");
        }
    }
}
