using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestProcessingRestrictionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guest_processing_restriction_receipts",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RestrictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    SelectedGuestVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingRestrictionVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingProjectionRevision = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveRestricted = table.Column<bool>(type: "boolean", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_processing_restriction_receipts", x => x.Id);
                    table.UniqueConstraint("AK_guest_processing_restriction_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_processing_restriction_receipts_versions", "\"ApprovalRevision\" >= 1 AND \"SelectedGuestVersion\" >= 1 AND \"ResultingProjectionRevision\" >= 1 AND ((\"Action\" = 1 AND \"ResultingRestrictionVersion\" = 1 AND \"EffectiveRestricted\") OR (\"Action\" = 2 AND \"ResultingRestrictionVersion\" >= 2))");
                });

            migrationBuilder.CreateTable(
                name: "guest_processing_restriction_state",
                schema: "guests",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ActiveRestrictionCount = table.Column<int>(type: "integer", nullable: false),
                    IsRestricted = table.Column<bool>(type: "boolean", nullable: false),
                    LastTransitionAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_processing_restriction_state", x => new { x.ScopeId, x.PropertyId, x.GuestId });
                    table.CheckConstraint("CK_guest_processing_restrictions_contract_version", "\"ContractVersion\" >= 1");
                    table.CheckConstraint("CK_guest_processing_restrictions_effective_state", "(\"ActiveRestrictionCount\" = 0 AND NOT \"IsRestricted\") OR (\"ActiveRestrictionCount\" > 0 AND \"IsRestricted\")");
                    table.CheckConstraint("CK_guest_processing_restrictions_revision", "\"Revision\" >= 0");
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "guests"."guest_processing_restriction_state"
                    ("ScopeId", "PropertyId", "GuestId", "ContractVersion",
                     "Revision", "ActiveRestrictionCount", "IsRestricted",
                     "LastTransitionAtUtc")
                SELECT
                    visible."ScopeId",
                    visible."PropertyId",
                    visible."GuestId",
                    1,
                    0,
                    0,
                    FALSE,
                    MIN(visible."InitializedAtUtc")
                FROM
                (
                    SELECT
                        profile."ScopeId",
                        profile."OriginPropertyId" AS "PropertyId",
                        profile."Id" AS "GuestId",
                        profile."CreatedAtUtc" AS "InitializedAtUtc"
                    FROM "guests"."guest_profiles" AS profile

                    UNION ALL

                    SELECT
                        stay."ScopeId",
                        stay."PropertyId",
                        stay."GuestId",
                        CURRENT_TIMESTAMP AS "InitializedAtUtc"
                    FROM "guests"."stay_history" AS stay
                    INNER JOIN "guests"."guest_profiles" AS profile
                        ON profile."ScopeId" = stay."ScopeId"
                        AND profile."Id" = stay."GuestId"
                    WHERE stay."IsCurrentParticipant"
                ) AS visible
                GROUP BY visible."ScopeId", visible."PropertyId", visible."GuestId";
                """);

            migrationBuilder.CreateTable(
                name: "guest_processing_restrictions",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplyCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplyApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    ApplySelectedGuestVersion = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    AppliedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleaseCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseApprovalRevision = table.Column<long>(type: "bigint", nullable: true),
                    ReleaseSelectedGuestVersion = table.Column<long>(type: "bigint", nullable: true),
                    ReleasedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_processing_restrictions", x => x.Id);
                    table.UniqueConstraint("AK_guest_processing_restrictions_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_processing_restrictions_apply_approval", "\"ApplyApprovalRevision\" >= 1 AND \"ApplySelectedGuestVersion\" >= 1");
                    table.CheckConstraint("CK_guest_processing_restrictions_lifecycle", "(\"Status\" = 1 AND \"ReleaseCaseId\" IS NULL AND \"ReleaseApprovalRevision\" IS NULL AND \"ReleaseSelectedGuestVersion\" IS NULL AND \"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR (\"Status\" = 2 AND \"ReleaseCaseId\" IS NOT NULL AND \"ReleaseApprovalRevision\" >= 1 AND \"ReleaseSelectedGuestVersion\" >= 1 AND \"ReleasedBy\" IS NOT NULL AND \"ReleasedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" >= \"AppliedAtUtc\" AND \"Version\" >= 2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_guest_processing_restriction_receipts_ScopeId_IdempotencyKey",
                schema: "guests",
                table: "guest_processing_restriction_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_processing_restriction_receipts_ScopeId_PropertyId_Ca~",
                schema: "guests",
                table: "guest_processing_restriction_receipts",
                columns: new[] { "ScopeId", "PropertyId", "CaseId", "ApprovalRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_guest_processing_restriction_receipts_ScopeId_PropertyId_Gu~",
                schema: "guests",
                table: "guest_processing_restriction_receipts",
                columns: new[] { "ScopeId", "PropertyId", "GuestId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_guest_processing_restriction_state_ScopeId_PropertyId_IsRes~",
                schema: "guests",
                table: "guest_processing_restriction_state",
                columns: new[] { "ScopeId", "PropertyId", "IsRestricted", "GuestId" });

            migrationBuilder.CreateIndex(
                name: "IX_guest_processing_restrictions_ScopeId_PropertyId_GuestId_Ap~",
                schema: "guests",
                table: "guest_processing_restrictions",
                columns: new[] { "ScopeId", "PropertyId", "GuestId", "ApplyCaseId", "ApplyApprovalRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_processing_restrictions_ScopeId_PropertyId_GuestId_St~",
                schema: "guests",
                table: "guest_processing_restrictions",
                columns: new[] { "ScopeId", "PropertyId", "GuestId", "Status", "AppliedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guest_processing_restriction_receipts",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "guest_processing_restriction_state",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "guest_processing_restrictions",
                schema: "guests");
        }
    }
}
