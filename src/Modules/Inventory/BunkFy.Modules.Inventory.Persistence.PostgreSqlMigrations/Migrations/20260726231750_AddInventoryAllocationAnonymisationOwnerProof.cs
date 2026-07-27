using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Inventory.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAllocationAnonymisationOwnerProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAtUtc",
                schema: "inventory",
                table: "allocations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymised",
                schema: "inventory",
                table: "allocations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "allocation_anonymisation_receipts",
                schema: "inventory",
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
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedAllocationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingAllocationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingReservationPseudonym = table.Column<Guid>(type: "uuid", nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    RemovedAmendmentDecisionCount = table.Column<int>(type: "integer", nullable: false),
                    ApprovalEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_anonymisation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_allocation_anonymisation_receipts_ScopeId_CanonicalSha256", x => new { x.ScopeId, x.CanonicalSha256 });
                    table.UniqueConstraint("AK_allocation_anonymisation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_allocation_anonymisation_receipts_actor", "length(trim(\"ActorId\")) > 0");
                    table.CheckConstraint("CK_allocation_anonymisation_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_allocation_anonymisation_receipts_counts", "\"RemovedAmendmentDecisionCount\" >= 0");
                    table.CheckConstraint("CK_allocation_anonymisation_receipts_digests", "char_length(\"ApprovalEvidenceSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_allocation_anonymisation_receipts_outcome", "\"Disposition\" = 1 AND \"Reason\" = 1");
                    table.CheckConstraint("CK_allocation_anonymisation_receipts_revisions", "\"ApprovalRevision\" >= 1 AND \"OperationRevision\" > \"ApprovalRevision\"");
                    table.CheckConstraint("CK_allocation_anonymisation_receipts_versions", "\"SelectedAllocationVersion\" >= 1 AND \"ResultingAllocationVersion\" = \"SelectedAllocationVersion\" + 1");
                    table.ForeignKey(
                        name: "FK_allocation_anonymisation_receipts_allocations_ScopeId_Alloc~",
                        columns: x => new { x.ScopeId, x.AllocationId },
                        principalSchema: "inventory",
                        principalTable: "allocations",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "allocation_anonymisation_tombstones",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingAllocationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingReservationPseudonym = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationPresent = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_anonymisation_tombstones", x => x.Id);
                    table.UniqueConstraint("AK_allocation_anonymisation_tombstones_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_allocation_anonymisation_tombstones_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_allocation_anonymisation_tombstones_receipt", "\"OwnerReceiptContractVersion\" >= 1 AND char_length(\"OwnerReceiptSha256\") = 64");
                    table.CheckConstraint("CK_allocation_anonymisation_tombstones_replay", "(\"LedgerEntryId\" IS NULL AND \"LastReplayedAtUtc\" IS NULL) OR (\"LedgerEntryId\" IS NOT NULL AND \"LastReplayedAtUtc\" IS NOT NULL AND \"LastReplayedAtUtc\" >= \"CompletedAtUtc\")");
                    table.CheckConstraint("CK_allocation_anonymisation_tombstones_revision", "\"Revision\" >= 1");
                    table.CheckConstraint("CK_allocation_anonymisation_tombstones_version", "\"ResultingAllocationVersion\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "allocation_operation_locks",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_operation_locks", x => x.Id);
                    table.UniqueConstraint("AK_allocation_operation_locks_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_allocation_operation_locks_revision", "\"Revision\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "allocation_anonymisation_restore_receipts",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantSequence = table.Column<long>(type: "bigint", nullable: false),
                    LedgerEntrySha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    OwnerReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultingAllocationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingReservationPseudonym = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationPresent = table.Column<bool>(type: "boolean", nullable: false),
                    OriginallyCompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TombstoneRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_anonymisation_restore_receipts", x => x.Id);
                    table.UniqueConstraint("AK_allocation_anonymisation_restore_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_allocation_anonymisation_restore_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_allocation_anonymisation_restore_receipts_coordinates", "\"TenantSequence\" >= 1 AND \"OwnerReceiptContractVersion\" >= 1 AND \"ResultingAllocationVersion\" >= 1 AND \"TombstoneRevision\" >= 1");
                    table.CheckConstraint("CK_allocation_anonymisation_restore_receipts_digests", "char_length(\"LedgerEntrySha256\") = 64 AND char_length(\"OwnerReceiptSha256\") = 64 AND char_length(\"CanonicalSha256\") = 64");
                    table.CheckConstraint("CK_allocation_anonymisation_restore_receipts_identity", "\"LedgerEntryId\" = \"Id\"");
                    table.CheckConstraint("CK_allocation_anonymisation_restore_receipts_times", "\"ReplayedAtUtc\" >= \"OriginallyCompletedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_allocation_anonymisation_restore_receipts_allocation_anonym~",
                        columns: x => new { x.ScopeId, x.AllocationId },
                        principalSchema: "inventory",
                        principalTable: "allocation_anonymisation_tombstones",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_allocations_anonymisation_state",
                schema: "inventory",
                table: "allocations",
                sql: "(\"IsAnonymised\" = TRUE AND \"AnonymisedAtUtc\" IS NOT NULL) OR (\"IsAnonymised\" = FALSE AND \"AnonymisedAtUtc\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_allocation_anonymisation_receipts_ScopeId_AllocationId",
                schema: "inventory",
                table: "allocation_anonymisation_receipts",
                columns: new[] { "ScopeId", "AllocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_allocation_anonymisation_receipts_ScopeId_CaseId_ApprovalRe~",
                schema: "inventory",
                table: "allocation_anonymisation_receipts",
                columns: new[] { "ScopeId", "CaseId", "ApprovalRevision", "OperationRevision", "AllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_allocation_anonymisation_receipts_ScopeId_IdempotencyKey",
                schema: "inventory",
                table: "allocation_anonymisation_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_allocation_anonymisation_receipts_ScopeId_PropertyId_Alloca~",
                schema: "inventory",
                table: "allocation_anonymisation_receipts",
                columns: new[] { "ScopeId", "PropertyId", "AllocationId", "ResultingAllocationVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_allocation_anonymisation_restore_receipts_ScopeId_Allocatio~",
                schema: "inventory",
                table: "allocation_anonymisation_restore_receipts",
                columns: new[] { "ScopeId", "AllocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_allocation_anonymisation_restore_receipts_ScopeId_PropertyI~",
                schema: "inventory",
                table: "allocation_anonymisation_restore_receipts",
                columns: new[] { "ScopeId", "PropertyId", "AllocationId", "LedgerEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_allocation_anonymisation_tombstones_ScopeId_LedgerEntryId",
                schema: "inventory",
                table: "allocation_anonymisation_tombstones",
                columns: new[] { "ScopeId", "LedgerEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_allocation_anonymisation_tombstones_ScopeId_PropertyId_Comp~",
                schema: "inventory",
                table: "allocation_anonymisation_tombstones",
                columns: new[] { "ScopeId", "PropertyId", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_allocation_operation_locks_ScopeId_AllocationId",
                schema: "inventory",
                table: "allocation_operation_locks",
                columns: new[] { "ScopeId", "AllocationId" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO inventory.allocation_operation_locks
                    ("Id", "AllocationId", "Revision", "ScopeId")
                SELECT
                    allocation."Id",
                    allocation."Id",
                    1,
                    allocation."ScopeId"
                FROM inventory.allocations AS allocation
                ON CONFLICT ("ScopeId", "AllocationId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocation_anonymisation_receipts",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "allocation_anonymisation_restore_receipts",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "allocation_operation_locks",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "allocation_anonymisation_tombstones",
                schema: "inventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_allocations_anonymisation_state",
                schema: "inventory",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                schema: "inventory",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "IsAnonymised",
                schema: "inventory",
                table: "allocations");
        }
    }
}
