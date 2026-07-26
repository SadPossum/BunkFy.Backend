using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionAnonymisationExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_anonymisation_tombstones_ScopeId_LedgerEntryId",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_anonymisation_tombstones_contract",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerReceiptSha256",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(64)",
                oldFixedLength: true,
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "LedgerEntrySha256",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(64)",
                oldFixedLength: true,
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "ActorId",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceSha256",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "ApprovalRevision",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "CaseId",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExecutionStartedAtUtc",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "OperationFenceSha256",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "OperationRevision",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PolicyEvidenceSha256",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkItemId",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE ingestion.anonymisation_tombstones
                SET "ContractVersion" = 2,
                    "Origin" = 2
                WHERE "ContractVersion" = 1;
                """);

            migrationBuilder.CreateTable(
                name: "anonymisation_receipts",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    OperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    SourceLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedSourceLinkVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingSourceLinkVersion = table.Column<long>(type: "bigint", nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    GraphRecordCount = table.Column<int>(type: "integer", nullable: false),
                    FingerprintCount = table.Column<int>(type: "integer", nullable: false),
                    RawPayloadCount = table.Column<int>(type: "integer", nullable: false),
                    ApprovalEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PolicyEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OperationFenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anonymisation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_anonymisation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_ingestion_anonymisation_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_ingestion_anonymisation_receipts_counts", "\"GraphRecordCount\" >= 1 AND \"FingerprintCount\" >= 1 AND \"RawPayloadCount\" >= 0");
                    table.CheckConstraint("CK_ingestion_anonymisation_receipts_outcome", "\"Disposition\" = 1 AND \"Reason\" = 1");
                    table.CheckConstraint("CK_ingestion_anonymisation_receipts_versions", "\"SelectedSourceLinkVersion\" >= 1 AND \"ResultingSourceLinkVersion\" = \"SelectedSourceLinkVersion\" + 1");
                    table.ForeignKey(
                        name: "FK_anonymisation_receipts_anonymisation_tombstones_ScopeId_Sou~",
                        columns: x => new { x.ScopeId, x.SourceLinkId },
                        principalSchema: "ingestion",
                        principalTable: "anonymisation_tombstones",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_tombstones_ScopeId_IdempotencyKey",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_tombstones_ScopeId_LedgerEntryId",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                columns: new[] { "ScopeId", "LedgerEntryId" },
                unique: true,
                filter: "\"LedgerEntryId\" <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_anonymisation_tombstones_contract",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                sql: "\"ContractVersion\" = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_anonymisation_tombstones_origin",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                sql: "\"Origin\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_receipts_ScopeId_IdempotencyKey",
                schema: "ingestion",
                table: "anonymisation_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_receipts_ScopeId_PropertyId_CompletedAtUtc",
                schema: "ingestion",
                table: "anonymisation_receipts",
                columns: new[] { "ScopeId", "PropertyId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_receipts_ScopeId_SourceLinkId",
                schema: "ingestion",
                table: "anonymisation_receipts",
                columns: new[] { "ScopeId", "SourceLinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_receipts_ScopeId_WorkItemId",
                schema: "ingestion",
                table: "anonymisation_receipts",
                columns: new[] { "ScopeId", "WorkItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM ingestion.anonymisation_tombstones
                        WHERE "Origin" = 1)
                       OR EXISTS (
                        SELECT 1
                        FROM ingestion.anonymisation_receipts)
                    THEN
                        RAISE EXCEPTION
                            'Cannot downgrade ingestion anonymisation execution after live execution evidence exists.';
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.DropTable(
                name: "anonymisation_receipts",
                schema: "ingestion");

            migrationBuilder.DropIndex(
                name: "IX_anonymisation_tombstones_ScopeId_IdempotencyKey",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropIndex(
                name: "IX_anonymisation_tombstones_ScopeId_LedgerEntryId",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_anonymisation_tombstones_contract",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_anonymisation_tombstones_origin",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.Sql(
                """
                UPDATE ingestion.anonymisation_tombstones
                SET "ContractVersion" = 1
                WHERE "ContractVersion" = 2;
                """);

            migrationBuilder.DropColumn(
                name: "ActorId",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceSha256",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "ApprovalRevision",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "CaseId",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "ExecutionStartedAtUtc",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "OperationFenceSha256",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "OperationRevision",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "PolicyEvidenceSha256",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.DropColumn(
                name: "WorkItemId",
                schema: "ingestion",
                table: "anonymisation_tombstones");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerReceiptSha256",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "LedgerEntrySha256",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_anonymisation_tombstones_ScopeId_LedgerEntryId",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                columns: new[] { "ScopeId", "LedgerEntryId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_anonymisation_tombstones_contract",
                schema: "ingestion",
                table: "anonymisation_tombstones",
                sql: "\"ContractVersion\" = 1");
        }
    }
}
