using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationProcessingRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservation_processing_restriction_receipts",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RestrictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    SelectedReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ResultingRestrictionVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingProjectionRevision = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveRestricted = table.Column<bool>(type: "boolean", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_processing_restriction_receipts", x => x.Id);
                    table.UniqueConstraint("AK_reservation_processing_restriction_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_processing_restriction_receipts_versions", "\"ApprovalRevision\" >= 1 AND \"SelectedReservationVersion\" >= 1 AND \"ContractVersion\" >= 1 AND \"ResultingProjectionRevision\" >= 1 AND ((\"Action\" = 1 AND \"ResultingRestrictionVersion\" = 1 AND \"EffectiveRestricted\") OR (\"Action\" = 2 AND \"ResultingRestrictionVersion\" >= 2))");
                });

            migrationBuilder.CreateTable(
                name: "reservation_processing_restriction_state",
                schema: "reservations",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectionOrdinal = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ActiveRestrictionCount = table.Column<int>(type: "integer", nullable: false),
                    IsRestricted = table.Column<bool>(type: "boolean", nullable: false),
                    LastTransitionAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_processing_restriction_state", x => new { x.ScopeId, x.PropertyId, x.ReservationId });
                    table.CheckConstraint("CK_reservation_processing_restriction_state_contract", "\"ContractVersion\" >= 1");
                    table.CheckConstraint("CK_reservation_processing_restriction_state_effective", "(\"ActiveRestrictionCount\" = 0 AND NOT \"IsRestricted\") OR (\"ActiveRestrictionCount\" > 0 AND \"IsRestricted\")");
                    table.CheckConstraint("CK_reservation_processing_restriction_state_revision", "\"Revision\" >= 0 AND \"ActiveRestrictionCount\" >= 0 AND \"ActiveRestrictionCount\" <= \"Revision\"");
                });

            migrationBuilder.CreateTable(
                name: "reservation_processing_restrictions",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplyCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplyApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    ApplySelectedReservationVersion = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    AppliedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleaseCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseApprovalRevision = table.Column<long>(type: "bigint", nullable: true),
                    ReleaseSelectedReservationVersion = table.Column<long>(type: "bigint", nullable: true),
                    ReleasedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_processing_restrictions", x => x.Id);
                    table.UniqueConstraint("AK_reservation_processing_restrictions_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_reservation_processing_restrictions_apply_approval", "\"ApplyApprovalRevision\" >= 1 AND \"ApplySelectedReservationVersion\" >= 1");
                    table.CheckConstraint("CK_reservation_processing_restrictions_lifecycle", "(\"Status\" = 1 AND \"ReleaseCaseId\" IS NULL AND \"ReleaseApprovalRevision\" IS NULL AND \"ReleaseSelectedReservationVersion\" IS NULL AND \"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR (\"Status\" = 2 AND \"ReleaseCaseId\" IS NOT NULL AND \"ReleaseApprovalRevision\" >= 1 AND \"ReleaseSelectedReservationVersion\" >= 1 AND \"ReleasedBy\" IS NOT NULL AND \"ReleasedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" >= \"AppliedAtUtc\" AND \"Version\" >= 2)");
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "reservations"."reservation_processing_restriction_state"
                    ("ScopeId", "PropertyId", "ReservationId", "ContractVersion",
                     "Revision", "ActiveRestrictionCount", "IsRestricted",
                     "LastTransitionAtUtc")
                SELECT
                    reservation."ScopeId",
                    reservation."PropertyId",
                    reservation."Id",
                    1,
                    0,
                    0,
                    FALSE,
                    reservation."CreatedAtUtc"
                FROM "reservations"."reservations" AS reservation;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_processing_restriction_receipts_ScopeId_EventId",
                schema: "reservations",
                table: "reservation_processing_restriction_receipts",
                columns: new[] { "ScopeId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_processing_restriction_receipts_ScopeId_Idempot~",
                schema: "reservations",
                table: "reservation_processing_restriction_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_processing_restriction_receipts_ScopeId_Proper~1",
                schema: "reservations",
                table: "reservation_processing_restriction_receipts",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_processing_restriction_receipts_ScopeId_Propert~",
                schema: "reservations",
                table: "reservation_processing_restriction_receipts",
                columns: new[] { "ScopeId", "PropertyId", "CaseId", "ApprovalRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_processing_restriction_state_ProjectionOrdinal",
                schema: "reservations",
                table: "reservation_processing_restriction_state",
                column: "ProjectionOrdinal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_processing_restriction_state_ScopeId_PropertyId~",
                schema: "reservations",
                table: "reservation_processing_restriction_state",
                columns: new[] { "ScopeId", "PropertyId", "IsRestricted", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_processing_restrictions_ScopeId_PropertyId_Res~1",
                schema: "reservations",
                table: "reservation_processing_restrictions",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "ReleaseCaseId", "ReleaseApprovalRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_processing_restrictions_ScopeId_PropertyId_Res~2",
                schema: "reservations",
                table: "reservation_processing_restrictions",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "Status", "AppliedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_processing_restrictions_ScopeId_PropertyId_Rese~",
                schema: "reservations",
                table: "reservation_processing_restrictions",
                columns: new[] { "ScopeId", "PropertyId", "ReservationId", "ApplyCaseId", "ApplyApprovalRevision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_processing_restriction_receipts",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservation_processing_restriction_state",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "reservation_processing_restrictions",
                schema: "reservations");
        }
    }
}
