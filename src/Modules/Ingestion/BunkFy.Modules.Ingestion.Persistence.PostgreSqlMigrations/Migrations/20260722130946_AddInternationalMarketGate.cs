using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInternationalMarketGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SourceVersion",
                schema: "ingestion",
                table: "property_projection",
                newName: "TopologySourceVersion");

            migrationBuilder.AddColumn<string>(
                name: "DataRegionId",
                schema: "ingestion",
                table: "property_projection",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JurisdictionPolicyId",
                schema: "ingestion",
                table: "property_projection",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JurisdictionPolicyVersion",
                schema: "ingestion",
                table: "property_projection",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingCountryCode",
                schema: "ingestion",
                table: "property_projection",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyActivatedAtUtc",
                schema: "ingestion",
                table: "property_projection",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyContentSha256",
                schema: "ingestion",
                table: "property_projection",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyEffectiveAtUtc",
                schema: "ingestion",
                table: "property_projection",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyExpiresAtUtc",
                schema: "ingestion",
                table: "property_projection",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PolicySourceVersion",
                schema: "ingestion",
                table: "property_projection",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingStatus",
                schema: "ingestion",
                table: "property_projection",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RetentionPolicyId",
                schema: "ingestion",
                table: "property_projection",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionPolicyVersion",
                schema: "ingestion",
                table: "property_projection",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferProfileId",
                schema: "ingestion",
                table: "property_projection",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JurisdictionPolicyId",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JurisdictionPolicyVersion",
                schema: "ingestion",
                table: "observation_receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyContentSha256",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyDataRegionId",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyEffectiveAtUtc",
                schema: "ingestion",
                table: "observation_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyEvaluatedAtUtc",
                schema: "ingestion",
                table: "observation_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyExpiresAtUtc",
                schema: "ingestion",
                table: "observation_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyOperatingCountryCode",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyProcessingSurface",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyPurposeCode",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyRetentionPolicyId",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PolicyRetentionPolicyVersion",
                schema: "ingestion",
                table: "observation_receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySourceProvenance",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyTransferProfileId",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "property_policy_acknowledgements",
                schema: "ingestion",
                columns: table => new
                {
                    AcknowledgementId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AcknowledgementVersion = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_policy_acknowledgements", x => new { x.PropertyId, x.AcknowledgementId, x.AcknowledgementVersion });
                    table.ForeignKey(
                        name: "FK_property_policy_acknowledgements_property_projection_Proper~",
                        column: x => x.PropertyId,
                        principalSchema: "ingestion",
                        principalTable: "property_projection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_property_projection_governance_policy",
                schema: "ingestion",
                table: "property_projection",
                sql: "(\"ProcessingStatus\" = 1 AND \"OperatingCountryCode\" IS NULL AND \"JurisdictionPolicyId\" IS NULL AND \"JurisdictionPolicyVersion\" IS NULL AND \"DataRegionId\" IS NULL AND \"TransferProfileId\" IS NULL AND \"RetentionPolicyId\" IS NULL AND \"RetentionPolicyVersion\" IS NULL AND \"PolicyContentSha256\" IS NULL AND \"PolicyEffectiveAtUtc\" IS NULL AND \"PolicyExpiresAtUtc\" IS NULL AND \"PolicyActivatedAtUtc\" IS NULL) OR (\"ProcessingStatus\" IN (2, 3) AND \"OperatingCountryCode\" IS NOT NULL AND \"JurisdictionPolicyId\" IS NOT NULL AND \"JurisdictionPolicyVersion\" IS NOT NULL AND \"DataRegionId\" IS NOT NULL AND \"TransferProfileId\" IS NOT NULL AND \"RetentionPolicyId\" IS NOT NULL AND \"RetentionPolicyVersion\" IS NOT NULL AND \"PolicyContentSha256\" IS NOT NULL AND \"PolicyEffectiveAtUtc\" IS NOT NULL AND \"PolicyExpiresAtUtc\" IS NOT NULL AND \"PolicyActivatedAtUtc\" IS NOT NULL AND \"JurisdictionPolicyVersion\" > 0 AND \"RetentionPolicyVersion\" > 0 AND char_length(\"OperatingCountryCode\") = 2 AND char_length(\"PolicyContentSha256\") = 64 AND \"PolicyEffectiveAtUtc\" < \"PolicyExpiresAtUtc\" AND \"PolicyActivatedAtUtc\" >= \"PolicyEffectiveAtUtc\" AND \"PolicyActivatedAtUtc\" < \"PolicyExpiresAtUtc\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_property_projection_processing_status",
                schema: "ingestion",
                table: "property_projection",
                sql: "\"ProcessingStatus\" BETWEEN 1 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_observation_receipts_policy_evidence",
                schema: "ingestion",
                table: "observation_receipts",
                sql: "(\"JurisdictionPolicyId\" IS NULL AND \"PolicyOperatingCountryCode\" IS NULL AND \"JurisdictionPolicyVersion\" IS NULL AND \"PolicyDataRegionId\" IS NULL AND \"PolicyTransferProfileId\" IS NULL AND \"PolicyRetentionPolicyId\" IS NULL AND \"PolicyRetentionPolicyVersion\" IS NULL AND \"PolicyContentSha256\" IS NULL AND \"PolicyPurposeCode\" IS NULL AND \"PolicyProcessingSurface\" IS NULL AND \"PolicySourceProvenance\" IS NULL AND \"PolicyEffectiveAtUtc\" IS NULL AND \"PolicyExpiresAtUtc\" IS NULL AND \"PolicyEvaluatedAtUtc\" IS NULL) OR (\"JurisdictionPolicyId\" IS NOT NULL AND \"PolicyOperatingCountryCode\" IS NOT NULL AND \"JurisdictionPolicyVersion\" IS NOT NULL AND \"PolicyDataRegionId\" IS NOT NULL AND \"PolicyTransferProfileId\" IS NOT NULL AND \"PolicyRetentionPolicyId\" IS NOT NULL AND \"PolicyRetentionPolicyVersion\" IS NOT NULL AND \"PolicyContentSha256\" IS NOT NULL AND \"PolicyPurposeCode\" IS NOT NULL AND \"PolicyProcessingSurface\" IS NOT NULL AND \"PolicySourceProvenance\" IS NOT NULL AND \"PolicyEffectiveAtUtc\" IS NOT NULL AND \"PolicyExpiresAtUtc\" IS NOT NULL AND \"PolicyEvaluatedAtUtc\" IS NOT NULL AND \"JurisdictionPolicyVersion\" > 0 AND \"PolicyRetentionPolicyVersion\" > 0 AND char_length(\"PolicyOperatingCountryCode\") = 2 AND char_length(\"PolicyContentSha256\") = 64 AND \"PolicyEffectiveAtUtc\" < \"PolicyExpiresAtUtc\" AND \"PolicyEvaluatedAtUtc\" >= \"PolicyEffectiveAtUtc\" AND \"PolicyEvaluatedAtUtc\" < \"PolicyExpiresAtUtc\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "property_policy_acknowledgements",
                schema: "ingestion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_property_projection_governance_policy",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropCheckConstraint(
                name: "CK_property_projection_processing_status",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropCheckConstraint(
                name: "CK_observation_receipts_policy_evidence",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "DataRegionId",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "JurisdictionPolicyId",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "JurisdictionPolicyVersion",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "OperatingCountryCode",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicyActivatedAtUtc",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicyContentSha256",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicyEffectiveAtUtc",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicyExpiresAtUtc",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicySourceVersion",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyId",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyVersion",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "TransferProfileId",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "JurisdictionPolicyId",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "JurisdictionPolicyVersion",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyContentSha256",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyDataRegionId",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyEffectiveAtUtc",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyEvaluatedAtUtc",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyExpiresAtUtc",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyOperatingCountryCode",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyProcessingSurface",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyPurposeCode",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionPolicyId",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionPolicyVersion",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicySourceProvenance",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "PolicyTransferProfileId",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.RenameColumn(
                name: "TopologySourceVersion",
                schema: "ingestion",
                table: "property_projection",
                newName: "SourceVersion");
        }
    }
}
