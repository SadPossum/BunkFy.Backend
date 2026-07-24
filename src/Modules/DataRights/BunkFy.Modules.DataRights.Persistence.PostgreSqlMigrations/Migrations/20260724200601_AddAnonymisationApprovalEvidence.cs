using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAnonymisationApprovalEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceContentSha256",
                schema: "data-rights",
                table: "cases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovalEvidenceEvaluatedAtUtc",
                schema: "data-rights",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceOperatingCountryCode",
                schema: "data-rights",
                table: "cases",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidencePolicyId",
                schema: "data-rights",
                table: "cases",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalEvidencePolicyVersion",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalEvidencePropertyId",
                schema: "data-rights",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ApprovalEvidencePropertyVersion",
                schema: "data-rights",
                table: "cases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidencePurposeCode",
                schema: "data-rights",
                table: "cases",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApprovalEvidenceRequiresDistinctExecutor",
                schema: "data-rights",
                table: "cases",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceRetentionPolicyId",
                schema: "data-rights",
                table: "cases",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalEvidenceRetentionPolicyVersion",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalEvidenceSchemaVersion",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceSourceProvenance",
                schema: "data-rights",
                table: "cases",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceSurface",
                schema: "data-rights",
                table: "cases",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "projection_rebuild_checkpoints",
                schema: "data-rights",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ProcessedCount = table.Column<long>(type: "bigint", nullable: false),
                    WrittenCount = table.Column<long>(type: "bigint", nullable: false),
                    SkippedCount = table.Column<long>(type: "bigint", nullable: false),
                    FailedCount = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionVersion = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projection_rebuild_checkpoints", x => new { x.ScopeId, x.ProjectionName, x.RunId });
                });

            migrationBuilder.CreateTable(
                name: "property_projection",
                schema: "data-rights",
                columns: table => new
                {
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsKnown = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    OperatingCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    JurisdictionPolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    JurisdictionPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    DataRegionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TransferProfileId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RetentionPolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RetentionPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    PolicyContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PolicyEffectiveAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PolicyExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PolicyActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TopologySourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    PolicySourceVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_projection", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_data_rights_property_projection_governance_policy", "(\"ProcessingStatus\" = 1 AND \"OperatingCountryCode\" IS NULL AND \"JurisdictionPolicyId\" IS NULL AND \"JurisdictionPolicyVersion\" IS NULL AND \"DataRegionId\" IS NULL AND \"TransferProfileId\" IS NULL AND \"RetentionPolicyId\" IS NULL AND \"RetentionPolicyVersion\" IS NULL AND \"PolicyContentSha256\" IS NULL AND \"PolicyEffectiveAtUtc\" IS NULL AND \"PolicyExpiresAtUtc\" IS NULL AND \"PolicyActivatedAtUtc\" IS NULL) OR (\"ProcessingStatus\" IN (2, 3) AND \"OperatingCountryCode\" IS NOT NULL AND \"JurisdictionPolicyId\" IS NOT NULL AND \"JurisdictionPolicyVersion\" IS NOT NULL AND \"DataRegionId\" IS NOT NULL AND \"TransferProfileId\" IS NOT NULL AND \"RetentionPolicyId\" IS NOT NULL AND \"RetentionPolicyVersion\" IS NOT NULL AND \"PolicyContentSha256\" IS NOT NULL AND \"PolicyEffectiveAtUtc\" IS NOT NULL AND \"PolicyExpiresAtUtc\" IS NOT NULL AND \"PolicyActivatedAtUtc\" IS NOT NULL AND \"JurisdictionPolicyVersion\" > 0 AND \"RetentionPolicyVersion\" > 0 AND char_length(\"OperatingCountryCode\") = 2 AND char_length(\"PolicyContentSha256\") = 64 AND \"PolicyEffectiveAtUtc\" < \"PolicyExpiresAtUtc\" AND \"PolicyActivatedAtUtc\" >= \"PolicyEffectiveAtUtc\" AND \"PolicyActivatedAtUtc\" < \"PolicyExpiresAtUtc\")");
                    table.CheckConstraint("CK_data_rights_property_projection_processing_status", "\"ProcessingStatus\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_data_rights_property_projection_versions", "\"TopologySourceVersion\" >= 0 AND \"PolicySourceVersion\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "property_policy_acknowledgements",
                schema: "data-rights",
                columns: table => new
                {
                    AcknowledgementId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AcknowledgementVersion = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_policy_acknowledgements", x => new { x.ScopeId, x.PropertyId, x.AcknowledgementId, x.AcknowledgementVersion });
                    table.ForeignKey(
                        name: "FK_property_policy_acknowledgements_property_projection_ScopeI~",
                        columns: x => new { x.ScopeId, x.PropertyId },
                        principalSchema: "data-rights",
                        principalTable: "property_projection",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."cases"
                        WHERE "Decision" = 1 AND "RequestedOperations" = 16
                    ) THEN
                        RAISE EXCEPTION
                            'Approved anonymisation cases require manual review before adding immutable policy evidence.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_approval_policy_evidence",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Decision\" = 1 AND \"RequestedOperations\" = 16 AND \"ApprovalEvidenceSchemaVersion\" = 1 AND \"ApprovalEvidencePropertyId\" = \"PropertyId\" AND \"ApprovalEvidencePropertyVersion\" > 0 AND \"ApprovalEvidenceOperatingCountryCode\" IS NOT NULL AND char_length(\"ApprovalEvidenceOperatingCountryCode\") = 2 AND \"ApprovalEvidencePolicyId\" IS NOT NULL AND \"ApprovalEvidencePolicyVersion\" > 0 AND \"ApprovalEvidenceRetentionPolicyId\" IS NOT NULL AND \"ApprovalEvidenceRetentionPolicyVersion\" > 0 AND \"ApprovalEvidenceContentSha256\" IS NOT NULL AND char_length(\"ApprovalEvidenceContentSha256\") = 64 AND \"ApprovalEvidencePurposeCode\" = 'data-rights-anonymisation' AND \"ApprovalEvidenceSurface\" = 'erasure' AND \"ApprovalEvidenceSourceProvenance\" = 'authorized-workspace-operator' AND \"ApprovalEvidenceEvaluatedAtUtc\" IS NOT NULL AND \"ApprovalEvidenceRequiresDistinctExecutor\" = TRUE) OR ((\"Decision\" <> 1 OR \"RequestedOperations\" <> 16) AND \"ApprovalEvidenceSchemaVersion\" IS NULL AND \"ApprovalEvidencePropertyId\" IS NULL AND \"ApprovalEvidencePropertyVersion\" IS NULL AND \"ApprovalEvidenceOperatingCountryCode\" IS NULL AND \"ApprovalEvidencePolicyId\" IS NULL AND \"ApprovalEvidencePolicyVersion\" IS NULL AND \"ApprovalEvidenceRetentionPolicyId\" IS NULL AND \"ApprovalEvidenceRetentionPolicyVersion\" IS NULL AND \"ApprovalEvidenceContentSha256\" IS NULL AND \"ApprovalEvidencePurposeCode\" IS NULL AND \"ApprovalEvidenceSurface\" IS NULL AND \"ApprovalEvidenceSourceProvenance\" IS NULL AND \"ApprovalEvidenceEvaluatedAtUtc\" IS NULL AND \"ApprovalEvidenceRequiresDistinctExecutor\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_property_projection_ScopeId_Status_Id",
                schema: "data-rights",
                table: "property_projection",
                columns: new[] { "ScopeId", "Status", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "projection_rebuild_checkpoints",
                schema: "data-rights");

            migrationBuilder.DropTable(
                name: "property_policy_acknowledgements",
                schema: "data-rights");

            migrationBuilder.DropTable(
                name: "property_projection",
                schema: "data-rights");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_approval_policy_evidence",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceContentSha256",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceEvaluatedAtUtc",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceOperatingCountryCode",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidencePolicyId",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidencePolicyVersion",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidencePropertyId",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidencePropertyVersion",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidencePurposeCode",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceRequiresDistinctExecutor",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceRetentionPolicyId",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceRetentionPolicyVersion",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceSchemaVersion",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceSourceProvenance",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceSurface",
                schema: "data-rights",
                table: "cases");
        }
    }
}
