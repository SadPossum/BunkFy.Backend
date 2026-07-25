using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDataRightsProcessingLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_execution_work_items_ScopeId_Id",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "Id" });

            migrationBuilder.CreateTable(
                name: "processing_ledger_entries",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    TenantSequence = table.Column<long>(type: "bigint", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    OperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<int>(type: "integer", nullable: false),
                    RoutingPropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordPseudonymKeyVersion = table.Column<int>(type: "integer", nullable: false),
                    RecordPseudonymSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    DispositionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PolicyEvidenceSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    PolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    PolicyContentSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RetentionPolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RetentionPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PreviousEntrySha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EntrySha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReplayOfLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersedesLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_ledger_entries", x => x.Id);
                    table.CheckConstraint("CK_data_rights_processing_ledger_chain", "\"PreviousEntrySha256\" ~ '^[0-9a-f]{64}$' AND \"EntrySha256\" ~ '^[0-9a-f]{64}$' AND ((\"TenantSequence\" = 1 AND \"PreviousEntrySha256\" = '0000000000000000000000000000000000000000000000000000000000000000') OR (\"TenantSequence\" > 1 AND \"PreviousEntrySha256\" <> '0000000000000000000000000000000000000000000000000000000000000000'))");
                    table.CheckConstraint("CK_data_rights_processing_ledger_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_data_rights_processing_ledger_operation", "\"Operation\" = 16");
                    table.CheckConstraint("CK_data_rights_processing_ledger_outcome", "length(trim(\"DispositionCode\")) > 0 AND length(trim(\"ReasonCode\")) > 0 AND \"CompletedAtUtc\" <> '-infinity'");
                    table.CheckConstraint("CK_data_rights_processing_ledger_policy", "\"PolicyEvidenceSchemaVersion\" = 1 AND length(trim(\"PolicyId\")) > 0 AND \"PolicyVersion\" >= 1 AND length(trim(\"RetentionPolicyId\")) > 0 AND \"RetentionPolicyVersion\" >= 1 AND \"PolicyContentSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_data_rights_processing_ledger_receipt", "\"OwnerReceiptContractVersion\" >= 1 AND \"OwnerReceiptSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_data_rights_processing_ledger_replay", "(\"ReplayOfLedgerEntryId\" IS NULL OR \"ReplayOfLedgerEntryId\" <> \"Id\") AND (\"SupersedesLedgerEntryId\" IS NULL OR \"SupersedesLedgerEntryId\" <> \"Id\") AND (\"ReplayOfLedgerEntryId\" IS NULL OR \"SupersedesLedgerEntryId\" IS NULL OR \"ReplayOfLedgerEntryId\" <> \"SupersedesLedgerEntryId\")");
                    table.CheckConstraint("CK_data_rights_processing_ledger_revisions", "\"ApprovalRevision\" >= 1 AND \"OperationRevision\" > \"ApprovalRevision\"");
                    table.CheckConstraint("CK_data_rights_processing_ledger_sequence", "\"TenantSequence\" >= 1");
                    table.CheckConstraint("CK_data_rights_processing_ledger_subject", "length(trim(\"OwnerKey\")) > 0 AND length(trim(\"RecordType\")) > 0 AND \"RecordPseudonymKeyVersion\" >= 1 AND \"RecordPseudonymSha256\" ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_processing_ledger_entries_execution_work_items_ScopeId_Work~",
                        columns: x => new { x.ScopeId, x.WorkItemId },
                        principalSchema: "data-rights",
                        principalTable: "execution_work_items",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_processing_ledger_entries_ScopeId_CaseId_OperationRevision_~",
                schema: "data-rights",
                table: "processing_ledger_entries",
                columns: new[] { "ScopeId", "CaseId", "OperationRevision", "OwnerKey", "RecordType", "RecordPseudonymSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processing_ledger_entries_ScopeId_EntrySha256",
                schema: "data-rights",
                table: "processing_ledger_entries",
                columns: new[] { "ScopeId", "EntrySha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processing_ledger_entries_ScopeId_OwnerReceiptId",
                schema: "data-rights",
                table: "processing_ledger_entries",
                columns: new[] { "ScopeId", "OwnerReceiptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processing_ledger_entries_ScopeId_RoutingPropertyId_Complet~",
                schema: "data-rights",
                table: "processing_ledger_entries",
                columns: new[] { "ScopeId", "RoutingPropertyId", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_processing_ledger_entries_ScopeId_TenantSequence",
                schema: "data-rights",
                table: "processing_ledger_entries",
                columns: new[] { "ScopeId", "TenantSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processing_ledger_entries_ScopeId_WorkItemId",
                schema: "data-rights",
                table: "processing_ledger_entries",
                columns: new[] { "ScopeId", "WorkItemId" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "data-rights".validate_processing_ledger_append()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    prior_digest character(64);
                BEGIN
                    IF NEW."TenantSequence" = 1 THEN
                        IF EXISTS (
                            SELECT 1
                            FROM "data-rights"."processing_ledger_entries"
                            WHERE "ScopeId" = NEW."ScopeId"
                            LIMIT 1
                        ) THEN
                            RAISE EXCEPTION 'data-rights processing ledger genesis already exists';
                        END IF;
                    ELSE
                        SELECT "EntrySha256"
                        INTO prior_digest
                        FROM "data-rights"."processing_ledger_entries"
                        WHERE "ScopeId" = NEW."ScopeId"
                          AND "TenantSequence" = NEW."TenantSequence" - 1
                        FOR KEY SHARE;

                        IF prior_digest IS NULL OR
                           prior_digest <> NEW."PreviousEntrySha256" THEN
                            RAISE EXCEPTION 'data-rights processing ledger chain is invalid';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE OR REPLACE FUNCTION "data-rights".prevent_processing_ledger_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'data-rights processing ledger entries are append-only';
                END;
                $function$;

                CREATE TRIGGER "TR_processing_ledger_entries_validate_append"
                BEFORE INSERT ON "data-rights"."processing_ledger_entries"
                FOR EACH ROW
                EXECUTE FUNCTION "data-rights".validate_processing_ledger_append();

                CREATE TRIGGER "TR_processing_ledger_entries_append_only"
                BEFORE UPDATE OR DELETE ON "data-rights"."processing_ledger_entries"
                FOR EACH ROW
                EXECUTE FUNCTION "data-rights".prevent_processing_ledger_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $block$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."processing_ledger_entries"
                        LIMIT 1
                    ) THEN
                        RAISE EXCEPTION 'cannot remove the data-rights processing ledger while evidence exists';
                    END IF;
                END;
                $block$;
                """);

            migrationBuilder.DropTable(
                name: "processing_ledger_entries",
                schema: "data-rights");

            migrationBuilder.Sql(
                """
                DROP FUNCTION IF EXISTS "data-rights".prevent_processing_ledger_mutation();
                DROP FUNCTION IF EXISTS "data-rights".validate_processing_ledger_append();
                """);

            migrationBuilder.DropUniqueConstraint(
                name: "AK_execution_work_items_ScopeId_Id",
                schema: "data-rights",
                table: "execution_work_items");
        }
    }
}
