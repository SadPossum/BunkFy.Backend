using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAnonymisationOwnerProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_members_lifecycle",
                schema: "staff",
                table: "staff_members");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAtUtc",
                schema: "staff",
                table: "staff_members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "staff_anonymisation_receipts",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    OperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    SelectedOperationLockRevision = table.Column<long>(type: "bigint", nullable: false),
                    ResultingOperationLockRevision = table.Column<long>(type: "bigint", nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    ApprovalEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StateBindingsSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_anonymisation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_anonymisation_receipts_ScopeId_CanonicalSha256", x => new { x.ScopeId, x.CanonicalSha256 });
                    table.UniqueConstraint("AK_staff_anonymisation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_anonymisation_receipts_actor", "length(trim(\"ActorId\")) > 0");
                    table.CheckConstraint("CK_staff_anonymisation_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_anonymisation_receipts_digests", "char_length(\"ApprovalEvidenceSha256\") = 64 AND char_length(\"StateBindingsSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_staff_anonymisation_receipts_lock_revisions", "\"SelectedOperationLockRevision\" >= 1 AND \"ResultingOperationLockRevision\" = \"SelectedOperationLockRevision\" + 1");
                    table.CheckConstraint("CK_staff_anonymisation_receipts_outcome", "\"Disposition\" = 1 AND \"Reason\" = 1");
                    table.CheckConstraint("CK_staff_anonymisation_receipts_revisions", "\"ApprovalRevision\" >= 1 AND \"OperationRevision\" > \"ApprovalRevision\"");
                    table.CheckConstraint("CK_staff_anonymisation_receipts_versions", "\"SelectedStaffVersion\" >= 1 AND \"ResultingStaffVersion\" = \"SelectedStaffVersion\" + 1");
                    table.ForeignKey(
                        name: "FK_staff_anonymisation_receipts_staff_members_ScopeId_StaffMem~",
                        columns: x => new { x.ScopeId, x.StaffMemberId },
                        principalSchema: "staff",
                        principalTable: "staff_members",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_anonymisation_tombstones",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Authority = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    LastReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_anonymisation_tombstones", x => x.Id);
                    table.UniqueConstraint("AK_staff_anonymisation_tombstones_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_anonymisation_tombstones_authority", "\"Authority\" = 1");
                    table.CheckConstraint("CK_staff_anonymisation_tombstones_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_anonymisation_tombstones_receipt_digest", "char_length(\"OwnerReceiptSha256\") = 64");
                    table.CheckConstraint("CK_staff_anonymisation_tombstones_revision", "\"Revision\" >= 1");
                    table.CheckConstraint("CK_staff_anonymisation_tombstones_state", "\"State\" = 1");
                    table.ForeignKey(
                        name: "FK_staff_anonymisation_tombstones_staff_members_ScopeId_Id",
                        columns: x => new { x.ScopeId, x.Id },
                        principalSchema: "staff",
                        principalTable: "staff_members",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_members_lifecycle",
                schema: "staff",
                table: "staff_members",
                sql: "(\"Status\" = 1 AND \"SuspendedAtUtc\" IS NULL AND \"DepartedAtUtc\" IS NULL AND \"DepartureEffectiveOn\" IS NULL AND \"AnonymisedAtUtc\" IS NULL) OR (\"Status\" = 2 AND \"SuspendedAtUtc\" IS NOT NULL AND \"DepartedAtUtc\" IS NULL AND \"DepartureEffectiveOn\" IS NULL AND \"AnonymisedAtUtc\" IS NULL) OR (\"Status\" = 3 AND \"SuspendedAtUtc\" IS NULL AND \"DepartedAtUtc\" IS NOT NULL AND \"DepartureEffectiveOn\" IS NOT NULL AND \"AnonymisedAtUtc\" IS NULL) OR (\"Status\" = 4 AND \"SuspendedAtUtc\" IS NULL AND \"DepartedAtUtc\" IS NOT NULL AND \"DepartureEffectiveOn\" IS NOT NULL AND \"AnonymisedAtUtc\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_staff_anonymisation_receipts_ScopeId_CaseId_ApprovalRevisio~",
                schema: "staff",
                table: "staff_anonymisation_receipts",
                columns: new[] { "ScopeId", "CaseId", "ApprovalRevision", "OperationRevision", "StaffMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_anonymisation_receipts_ScopeId_IdempotencyKey",
                schema: "staff",
                table: "staff_anonymisation_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_anonymisation_receipts_ScopeId_StaffMemberId_Resultin~",
                schema: "staff",
                table: "staff_anonymisation_receipts",
                columns: new[] { "ScopeId", "StaffMemberId", "ResultingStaffVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_anonymisation_tombstones_ScopeId_CompletedAtUtc_Id",
                schema: "staff",
                table: "staff_anonymisation_tombstones",
                columns: new[] { "ScopeId", "CompletedAtUtc", "Id" });

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "staff".prevent_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'Staff receipts are append-only';
                END;
                $function$;

                CREATE TRIGGER "TR_staff_data_rights_correction_receipts_append_only"
                BEFORE UPDATE OR DELETE ON "staff"."data_rights_correction_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_receipt_mutation();

                CREATE TRIGGER "TR_staff_processing_restriction_receipts_append_only"
                BEFORE UPDATE OR DELETE ON "staff"."staff_processing_restriction_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_receipt_mutation();

                CREATE TRIGGER "TR_staff_employment_governance_receipts_append_only"
                BEFORE UPDATE OR DELETE ON "staff"."staff_employment_governance_change_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_receipt_mutation();

                CREATE TRIGGER "TR_staff_data_hold_receipts_append_only"
                BEFORE UPDATE OR DELETE ON "staff"."staff_data_hold_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_receipt_mutation();

                CREATE TRIGGER "TR_staff_anonymisation_receipts_append_only"
                BEFORE UPDATE OR DELETE ON "staff"."staff_anonymisation_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_receipt_mutation();
                """);
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
                        FROM "staff"."staff_anonymisation_receipts"
                    ) OR EXISTS (
                        SELECT 1
                        FROM "staff"."staff_anonymisation_tombstones"
                    ) OR EXISTS (
                        SELECT 1
                        FROM "staff"."staff_members"
                        WHERE "Status" = 4
                           OR "AnonymisedAtUtc" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while Staff anonymisation state or proof exists.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "staff_anonymisation_receipts",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_anonymisation_tombstones",
                schema: "staff");

            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_staff_data_rights_correction_receipts_append_only"
                    ON "staff"."data_rights_correction_receipts";
                DROP TRIGGER IF EXISTS
                    "TR_staff_processing_restriction_receipts_append_only"
                    ON "staff"."staff_processing_restriction_receipts";
                DROP TRIGGER IF EXISTS
                    "TR_staff_employment_governance_receipts_append_only"
                    ON "staff"."staff_employment_governance_change_receipts";
                DROP TRIGGER IF EXISTS
                    "TR_staff_data_hold_receipts_append_only"
                    ON "staff"."staff_data_hold_receipts";
                DROP FUNCTION IF EXISTS "staff".prevent_receipt_mutation();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_members_lifecycle",
                schema: "staff",
                table: "staff_members");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                schema: "staff",
                table: "staff_members");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_members_lifecycle",
                schema: "staff",
                table: "staff_members",
                sql: "(\"Status\" = 1 AND \"SuspendedAtUtc\" IS NULL AND \"DepartedAtUtc\" IS NULL AND \"DepartureEffectiveOn\" IS NULL) OR (\"Status\" = 2 AND \"SuspendedAtUtc\" IS NOT NULL AND \"DepartedAtUtc\" IS NULL AND \"DepartureEffectiveOn\" IS NULL) OR (\"Status\" = 3 AND \"SuspendedAtUtc\" IS NULL AND \"DepartedAtUtc\" IS NOT NULL AND \"DepartureEffectiveOn\" IS NOT NULL)");
        }
    }
}
