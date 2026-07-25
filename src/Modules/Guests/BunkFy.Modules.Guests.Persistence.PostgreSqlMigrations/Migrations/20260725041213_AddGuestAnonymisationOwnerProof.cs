using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestAnonymisationOwnerProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_guest_profiles_lifecycle",
                schema: "guests",
                table: "guest_profiles");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonymisedAtUtc",
                schema: "guests",
                table: "guest_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "guest_anonymisation_receipts",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutingPropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    OperationRevision = table.Column<long>(type: "bigint", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedGuestVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingGuestVersion = table.Column<long>(type: "bigint", nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    AffectedPropertyCount = table.Column<int>(type: "integer", nullable: false),
                    ApprovalEvidenceSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    PolicySetSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_anonymisation_receipts", x => x.Id);
                    table.UniqueConstraint("AK_guest_anonymisation_receipts_ScopeId_CanonicalSha256", x => new { x.ScopeId, x.CanonicalSha256 });
                    table.UniqueConstraint("AK_guest_anonymisation_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_anonymisation_receipts_actor", "length(trim(\"ActorId\")) > 0");
                    table.CheckConstraint("CK_guest_anonymisation_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_guest_anonymisation_receipts_digests", "\"ApprovalEvidenceSha256\" ~ '^[0-9a-f]{64}$' AND \"PolicySetSha256\" ~ '^[0-9a-f]{64}$' AND \"CanonicalSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_guest_anonymisation_receipts_outcome", "\"Disposition\" = 1 AND \"Reason\" = 1");
                    table.CheckConstraint("CK_guest_anonymisation_receipts_property_count", "\"AffectedPropertyCount\" BETWEEN 1 AND 256");
                    table.CheckConstraint("CK_guest_anonymisation_receipts_revisions", "\"ApprovalRevision\" >= 1 AND \"OperationRevision\" > \"ApprovalRevision\"");
                    table.CheckConstraint("CK_guest_anonymisation_receipts_versions", "\"SelectedGuestVersion\" >= 1 AND \"ResultingGuestVersion\" = \"SelectedGuestVersion\" + 1");
                    table.ForeignKey(
                        name: "FK_guest_anonymisation_receipts_guest_profiles_ScopeId_GuestId",
                        columns: x => new { x.ScopeId, x.GuestId },
                        principalSchema: "guests",
                        principalTable: "guest_profiles",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guest_operation_locks",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceKind = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_operation_locks", x => x.Id);
                    table.UniqueConstraint("AK_guest_operation_locks_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_operation_locks_resource_kind", "\"ResourceKind\" IN (1, 2)");
                    table.CheckConstraint("CK_guest_operation_locks_revision", "\"Revision\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "guest_anonymisation_tombstones",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerReceiptSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    LastReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_anonymisation_tombstones", x => x.Id);
                    table.UniqueConstraint("AK_guest_anonymisation_tombstones_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_anonymisation_tombstones_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_guest_anonymisation_tombstones_receipt_digest", "\"OwnerReceiptSha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_guest_anonymisation_tombstones_revision", "\"Revision\" >= 1");
                    table.CheckConstraint("CK_guest_anonymisation_tombstones_state", "\"State\" = 1");
                    table.ForeignKey(
                        name: "FK_guest_anonymisation_tombstones_guest_anonymisation_receipts~",
                        columns: x => new { x.ScopeId, x.OwnerReceiptSha256 },
                        principalSchema: "guests",
                        principalTable: "guest_anonymisation_receipts",
                        principalColumns: new[] { "ScopeId", "CanonicalSha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guest_anonymisation_tombstones_guest_profiles_ScopeId_Id",
                        columns: x => new { x.ScopeId, x.Id },
                        principalSchema: "guests",
                        principalTable: "guest_profiles",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_guest_profiles_lifecycle",
                schema: "guests",
                table: "guest_profiles",
                sql: "(\"Status\" = 1 AND \"ArchivedAtUtc\" IS NULL AND \"AnonymisedAtUtc\" IS NULL) OR (\"Status\" = 2 AND \"ArchivedAtUtc\" IS NOT NULL AND \"ArchivedAtUtc\" >= \"CreatedAtUtc\" AND \"AnonymisedAtUtc\" IS NULL) OR (\"Status\" = 3 AND \"ArchivedAtUtc\" IS NULL AND \"AnonymisedAtUtc\" IS NOT NULL AND \"AnonymisedAtUtc\" >= \"CreatedAtUtc\")");

            migrationBuilder.CreateIndex(
                name: "IX_guest_anonymisation_receipts_ScopeId_CaseId_ApprovalRevisio~",
                schema: "guests",
                table: "guest_anonymisation_receipts",
                columns: new[] { "ScopeId", "CaseId", "ApprovalRevision", "OperationRevision", "GuestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_anonymisation_receipts_ScopeId_GuestId_ResultingGuest~",
                schema: "guests",
                table: "guest_anonymisation_receipts",
                columns: new[] { "ScopeId", "GuestId", "ResultingGuestVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_anonymisation_receipts_ScopeId_IdempotencyKey",
                schema: "guests",
                table: "guest_anonymisation_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_anonymisation_tombstones_ScopeId_CompletedAtUtc_Id",
                schema: "guests",
                table: "guest_anonymisation_tombstones",
                columns: new[] { "ScopeId", "CompletedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_guest_anonymisation_tombstones_ScopeId_OwnerReceiptSha256",
                schema: "guests",
                table: "guest_anonymisation_tombstones",
                columns: new[] { "ScopeId", "OwnerReceiptSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_operation_locks_ScopeId_ResourceKind_ResourceId",
                schema: "guests",
                table: "guest_operation_locks",
                columns: new[] { "ScopeId", "ResourceKind", "ResourceId" },
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
                        FROM guests.guest_profiles
                        WHERE "Status" = 3
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade Guests while anonymised profiles exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "guest_anonymisation_tombstones",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "guest_operation_locks",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "guest_anonymisation_receipts",
                schema: "guests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_guest_profiles_lifecycle",
                schema: "guests",
                table: "guest_profiles");

            migrationBuilder.DropColumn(
                name: "AnonymisedAtUtc",
                schema: "guests",
                table: "guest_profiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_guest_profiles_lifecycle",
                schema: "guests",
                table: "guest_profiles",
                sql: "(\"Status\" = 1 AND \"ArchivedAtUtc\" IS NULL) OR (\"Status\" = 2 AND \"ArchivedAtUtc\" IS NOT NULL AND \"ArchivedAtUtc\" >= \"CreatedAtUtc\")");
        }
    }
}
