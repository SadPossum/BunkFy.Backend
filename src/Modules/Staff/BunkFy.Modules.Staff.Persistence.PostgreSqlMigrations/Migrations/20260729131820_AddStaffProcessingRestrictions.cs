using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffProcessingRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_processing_restriction_receipts",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RestrictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    SelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_staff_processing_restriction_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_processing_restriction_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_processing_restriction_receipts_versions", "\"ApprovalRevision\" >= 1 AND \"SelectedStaffVersion\" >= 1 AND \"ResultingProjectionRevision\" >= 1 AND ((\"Action\" = 1 AND \"ResultingRestrictionVersion\" = 1 AND \"EffectiveRestricted\") OR (\"Action\" = 2 AND \"ResultingRestrictionVersion\" >= 2))");
                });

            migrationBuilder.CreateTable(
                name: "staff_processing_restriction_state",
                schema: "staff",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_staff_processing_restriction_state", x => new { x.ScopeId, x.StaffMemberId });
                    table.CheckConstraint("CK_staff_processing_restrictions_contract_version", "\"ContractVersion\" >= 1");
                    table.CheckConstraint("CK_staff_processing_restrictions_effective_state", "(\"ActiveRestrictionCount\" = 0 AND NOT \"IsRestricted\") OR (\"ActiveRestrictionCount\" > 0 AND \"IsRestricted\")");
                    table.CheckConstraint("CK_staff_processing_restrictions_revision", "\"Revision\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "staff_processing_restrictions",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplyCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplyApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    ApplySelectedStaffVersion = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    AppliedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleaseCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseApprovalRevision = table.Column<long>(type: "bigint", nullable: true),
                    ReleaseSelectedStaffVersion = table.Column<long>(type: "bigint", nullable: true),
                    ReleasedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_processing_restrictions", x => x.Id);
                    table.UniqueConstraint("AK_staff_processing_restrictions_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_processing_restrictions_apply_approval", "\"ApplyApprovalRevision\" >= 1 AND \"ApplySelectedStaffVersion\" >= 1");
                    table.CheckConstraint("CK_staff_processing_restrictions_lifecycle", "(\"Status\" = 1 AND \"ReleaseCaseId\" IS NULL AND \"ReleaseApprovalRevision\" IS NULL AND \"ReleaseSelectedStaffVersion\" IS NULL AND \"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR (\"Status\" = 2 AND \"ReleaseCaseId\" IS NOT NULL AND \"ReleaseApprovalRevision\" >= 1 AND \"ReleaseSelectedStaffVersion\" >= 1 AND \"ReleasedBy\" IS NOT NULL AND \"ReleasedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" >= \"AppliedAtUtc\" AND \"Version\" >= 2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_processing_restriction_receipts_ScopeId_CaseId_Approv~",
                schema: "staff",
                table: "staff_processing_restriction_receipts",
                columns: new[] { "ScopeId", "CaseId", "ApprovalRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_processing_restriction_receipts_ScopeId_IdempotencyKey",
                schema: "staff",
                table: "staff_processing_restriction_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_processing_restriction_receipts_ScopeId_StaffMemberId~",
                schema: "staff",
                table: "staff_processing_restriction_receipts",
                columns: new[] { "ScopeId", "StaffMemberId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_processing_restriction_state_ProjectionOrdinal",
                schema: "staff",
                table: "staff_processing_restriction_state",
                column: "ProjectionOrdinal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_processing_restriction_state_ScopeId_IsRestricted_Sta~",
                schema: "staff",
                table: "staff_processing_restriction_state",
                columns: new[] { "ScopeId", "IsRestricted", "StaffMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_processing_restrictions_ScopeId_StaffMemberId_ApplyCa~",
                schema: "staff",
                table: "staff_processing_restrictions",
                columns: new[] { "ScopeId", "StaffMemberId", "ApplyCaseId", "ApplyApprovalRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_processing_restrictions_ScopeId_StaffMemberId_Release~",
                schema: "staff",
                table: "staff_processing_restrictions",
                columns: new[] { "ScopeId", "StaffMemberId", "ReleaseCaseId", "ReleaseApprovalRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_processing_restrictions_ScopeId_StaffMemberId_Status_~",
                schema: "staff",
                table: "staff_processing_restrictions",
                columns: new[] { "ScopeId", "StaffMemberId", "Status", "AppliedAtUtc" });

            migrationBuilder.Sql(
                """
                INSERT INTO "staff"."staff_processing_restriction_state"
                    ("ScopeId", "StaffMemberId", "ContractVersion", "Revision",
                     "ActiveRestrictionCount", "IsRestricted", "LastTransitionAtUtc")
                SELECT
                    member."ScopeId",
                    member."Id",
                    1,
                    0,
                    0,
                    FALSE,
                    member."CreatedAtUtc"
                FROM "staff"."staff_members" AS member
                ON CONFLICT ("ScopeId", "StaffMemberId") DO NOTHING;
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
                        FROM "staff"."staff_processing_restrictions"
                    ) OR EXISTS (
                        SELECT 1
                        FROM "staff"."staff_processing_restriction_receipts"
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while Staff processing-restriction records exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "staff_processing_restriction_receipts",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_processing_restriction_state",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "staff_processing_restrictions",
                schema: "staff");
        }
    }
}
