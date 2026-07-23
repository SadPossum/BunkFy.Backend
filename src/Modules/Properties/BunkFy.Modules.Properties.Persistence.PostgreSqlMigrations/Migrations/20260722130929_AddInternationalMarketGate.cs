using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Properties.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInternationalMarketGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataRegionId",
                schema: "properties",
                table: "properties",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JurisdictionPolicyId",
                schema: "properties",
                table: "properties",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JurisdictionPolicyVersion",
                schema: "properties",
                table: "properties",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingCountryCode",
                schema: "properties",
                table: "properties",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyActivatedAtUtc",
                schema: "properties",
                table: "properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyContentSha256",
                schema: "properties",
                table: "properties",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyEffectiveAtUtc",
                schema: "properties",
                table: "properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyExpiresAtUtc",
                schema: "properties",
                table: "properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingState",
                schema: "properties",
                table: "properties",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RetentionPolicyId",
                schema: "properties",
                table: "properties",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionPolicyVersion",
                schema: "properties",
                table: "properties",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferProfileId",
                schema: "properties",
                table: "properties",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "property_governance_acknowledgements",
                schema: "properties",
                columns: table => new
                {
                    AcknowledgementId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AcknowledgementVersion = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_governance_acknowledgements", x => new { x.PropertyId, x.AcknowledgementId, x.AcknowledgementVersion });
                    table.ForeignKey(
                        name: "FK_property_governance_acknowledgements_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "properties",
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "property_governance_revisions",
                schema: "properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyVersion = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    DecisionReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousOperatingCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    PreviousJurisdictionPolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PreviousJurisdictionPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    PreviousDataRegionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PreviousTransferProfileId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PreviousRetentionPolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PreviousRetentionPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    PreviousPolicyContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviousAcknowledgementSetSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CurrentOperatingCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CurrentJurisdictionPolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CurrentJurisdictionPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    CurrentDataRegionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CurrentTransferProfileId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CurrentRetentionPolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CurrentRetentionPolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    CurrentPolicyContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CurrentAcknowledgementSetSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_governance_revisions", x => x.Id);
                    table.CheckConstraint("CK_property_governance_revision_action", "\"Action\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_property_governance_revision_version", "\"PropertyVersion\" >= 2");
                    table.ForeignKey(
                        name: "FK_property_governance_revisions_properties_ScopeId_PropertyId",
                        columns: x => new { x.ScopeId, x.PropertyId },
                        principalSchema: "properties",
                        principalTable: "properties",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_governance_binding",
                schema: "properties",
                table: "properties",
                sql: "(\"ProcessingState\" = 1 AND \"OperatingCountryCode\" IS NULL AND \"JurisdictionPolicyId\" IS NULL AND \"JurisdictionPolicyVersion\" IS NULL AND \"DataRegionId\" IS NULL AND \"TransferProfileId\" IS NULL AND \"RetentionPolicyId\" IS NULL AND \"RetentionPolicyVersion\" IS NULL AND \"PolicyContentSha256\" IS NULL AND \"PolicyEffectiveAtUtc\" IS NULL AND \"PolicyExpiresAtUtc\" IS NULL AND \"PolicyActivatedAtUtc\" IS NULL) OR (\"ProcessingState\" IN (2, 3) AND \"OperatingCountryCode\" IS NOT NULL AND \"JurisdictionPolicyId\" IS NOT NULL AND \"JurisdictionPolicyVersion\" IS NOT NULL AND \"DataRegionId\" IS NOT NULL AND \"TransferProfileId\" IS NOT NULL AND \"RetentionPolicyId\" IS NOT NULL AND \"RetentionPolicyVersion\" IS NOT NULL AND \"PolicyContentSha256\" IS NOT NULL AND \"PolicyEffectiveAtUtc\" IS NOT NULL AND \"PolicyExpiresAtUtc\" IS NOT NULL AND \"PolicyActivatedAtUtc\" IS NOT NULL AND \"JurisdictionPolicyVersion\" > 0 AND \"RetentionPolicyVersion\" > 0 AND char_length(\"OperatingCountryCode\") = 2 AND char_length(\"PolicyContentSha256\") = 64 AND \"PolicyEffectiveAtUtc\" < \"PolicyExpiresAtUtc\" AND \"PolicyActivatedAtUtc\" >= \"PolicyEffectiveAtUtc\" AND \"PolicyActivatedAtUtc\" < \"PolicyExpiresAtUtc\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_processing_state",
                schema: "properties",
                table: "properties",
                sql: "\"ProcessingState\" BETWEEN 1 AND 3");

            migrationBuilder.CreateIndex(
                name: "IX_property_governance_revisions_ScopeId_PropertyId_OccurredAt~",
                schema: "properties",
                table: "property_governance_revisions",
                columns: new[] { "ScopeId", "PropertyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_property_governance_revisions_ScopeId_PropertyId_PropertyVe~",
                schema: "properties",
                table: "property_governance_revisions",
                columns: new[] { "ScopeId", "PropertyId", "PropertyVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "property_governance_acknowledgements",
                schema: "properties");

            migrationBuilder.DropTable(
                name: "property_governance_revisions",
                schema: "properties");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_governance_binding",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_processing_state",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "DataRegionId",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "JurisdictionPolicyId",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "JurisdictionPolicyVersion",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "OperatingCountryCode",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "PolicyActivatedAtUtc",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "PolicyContentSha256",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "PolicyEffectiveAtUtc",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "PolicyExpiresAtUtc",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "ProcessingState",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyId",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyVersion",
                schema: "properties",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "TransferProfileId",
                schema: "properties",
                table: "properties");
        }
    }
}
