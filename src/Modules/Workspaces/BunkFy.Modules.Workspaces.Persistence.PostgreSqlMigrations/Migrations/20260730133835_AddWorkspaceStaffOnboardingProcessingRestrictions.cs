using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceStaffOnboardingProcessingRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_onboarding_processing_restriction_receipts",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RestrictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    SelectedOnboardingVersion = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_staff_onboarding_processing_restriction_receipts", x => x.Id);
                    table.UniqueConstraint("AK_staff_onboarding_processing_restriction_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_ws_onboarding_restriction_receipt_versions", "\"ApprovalRevision\" >= 1 AND \"SelectedOnboardingVersion\" >= 1 AND \"ResultingProjectionRevision\" >= 1 AND ((\"Action\" = 1 AND \"ResultingRestrictionVersion\" = 1 AND \"EffectiveRestricted\") OR (\"Action\" = 2 AND \"ResultingRestrictionVersion\" >= 2))");
                });

            migrationBuilder.CreateTable(
                name: "staff_onboarding_processing_restriction_state",
                schema: "workspaces",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_staff_onboarding_processing_restriction_state", x => new { x.ScopeId, x.ApplicationId });
                    table.CheckConstraint("CK_ws_onboarding_restriction_contract", "\"ContractVersion\" >= 1");
                    table.CheckConstraint("CK_ws_onboarding_restriction_revision", "\"Revision\" >= 0");
                    table.CheckConstraint("CK_ws_onboarding_restriction_state", "(\"ActiveRestrictionCount\" = 0 AND NOT \"IsRestricted\") OR (\"ActiveRestrictionCount\" > 0 AND \"IsRestricted\")");
                });

            migrationBuilder.CreateTable(
                name: "staff_onboarding_processing_restrictions",
                schema: "workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplyCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplyApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    ApplySelectedOnboardingVersion = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    AppliedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleaseCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleaseApprovalRevision = table.Column<long>(type: "bigint", nullable: true),
                    ReleaseSelectedOnboardingVersion = table.Column<long>(type: "bigint", nullable: true),
                    ReleasedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_onboarding_processing_restrictions", x => x.Id);
                    table.UniqueConstraint("AK_staff_onboarding_processing_restrictions_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_ws_onboarding_restrictions_apply", "\"ApplyApprovalRevision\" >= 1 AND \"ApplySelectedOnboardingVersion\" >= 1");
                    table.CheckConstraint("CK_ws_onboarding_restrictions_lifecycle", "(\"Status\" = 1 AND \"ReleaseCaseId\" IS NULL AND \"ReleaseApprovalRevision\" IS NULL AND \"ReleaseSelectedOnboardingVersion\" IS NULL AND \"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR (\"Status\" = 2 AND \"ReleaseCaseId\" IS NOT NULL AND \"ReleaseApprovalRevision\" >= 1 AND \"ReleaseSelectedOnboardingVersion\" >= 1 AND \"ReleasedBy\" IS NOT NULL AND \"ReleasedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" >= \"AppliedAtUtc\" AND \"Version\" >= 2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_processing_restriction_receipts_ScopeId_Ap~",
                schema: "workspaces",
                table: "staff_onboarding_processing_restriction_receipts",
                columns: new[] { "ScopeId", "ApplicationId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_processing_restriction_receipts_ScopeId_Ca~",
                schema: "workspaces",
                table: "staff_onboarding_processing_restriction_receipts",
                columns: new[] { "ScopeId", "CaseId", "ApprovalRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_processing_restriction_receipts_ScopeId_Id~",
                schema: "workspaces",
                table: "staff_onboarding_processing_restriction_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_processing_restriction_state_ProjectionOrd~",
                schema: "workspaces",
                table: "staff_onboarding_processing_restriction_state",
                column: "ProjectionOrdinal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_processing_restriction_state_ScopeId_IsRes~",
                schema: "workspaces",
                table: "staff_onboarding_processing_restriction_state",
                columns: new[] { "ScopeId", "IsRestricted", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_processing_restrictions_ScopeId_Applicati~1",
                schema: "workspaces",
                table: "staff_onboarding_processing_restrictions",
                columns: new[] { "ScopeId", "ApplicationId", "ReleaseCaseId", "ReleaseApprovalRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_processing_restrictions_ScopeId_Applicati~2",
                schema: "workspaces",
                table: "staff_onboarding_processing_restrictions",
                columns: new[] { "ScopeId", "ApplicationId", "Status", "AppliedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_processing_restrictions_ScopeId_Applicatio~",
                schema: "workspaces",
                table: "staff_onboarding_processing_restrictions",
                columns: new[] { "ScopeId", "ApplicationId", "ApplyCaseId", "ApplyApprovalRevision" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO
                    workspaces.staff_onboarding_processing_restriction_state
                    (
                        "ScopeId",
                        "ApplicationId",
                        "ContractVersion",
                        "Revision",
                        "ActiveRestrictionCount",
                        "IsRestricted",
                        "LastTransitionAtUtc"
                    )
                SELECT
                    "ScopeId",
                    "Id",
                    1,
                    0,
                    0,
                    FALSE,
                    "LastChangedAtUtc"
                FROM workspaces.staff_onboarding_applications
                ON CONFLICT ("ScopeId", "ApplicationId") DO NOTHING;

                CREATE TRIGGER
                    "TR_staff_onboarding_restriction_receipts_append_only"
                BEFORE UPDATE OR DELETE
                ON
                    workspaces.staff_onboarding_processing_restriction_receipts
                FOR EACH ROW
                EXECUTE FUNCTION workspaces.prevent_receipt_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_staff_onboarding_restriction_receipts_append_only"
                    ON
                    workspaces.staff_onboarding_processing_restriction_receipts;
                """);

            migrationBuilder.DropTable(
                name: "staff_onboarding_processing_restriction_receipts",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "staff_onboarding_processing_restriction_state",
                schema: "workspaces");

            migrationBuilder.DropTable(
                name: "staff_onboarding_processing_restrictions",
                schema: "workspaces");
        }
    }
}
