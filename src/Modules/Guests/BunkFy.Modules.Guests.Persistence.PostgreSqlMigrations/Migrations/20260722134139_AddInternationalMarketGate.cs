using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddInternationalMarketGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_guests_property_projection_version",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "guests",
                table: "property_projection",
                newName: "TopologySourceVersion");

            migrationBuilder.AddColumn<string>(
                name: "DataRegionId",
                schema: "guests",
                table: "property_projection",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsKnown",
                schema: "guests",
                table: "property_projection",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE guests.property_projection SET \"IsKnown\" = TRUE;");

            migrationBuilder.AddColumn<string>(
                name: "JurisdictionPolicyId",
                schema: "guests",
                table: "property_projection",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JurisdictionPolicyVersion",
                schema: "guests",
                table: "property_projection",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingCountryCode",
                schema: "guests",
                table: "property_projection",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyActivatedAtUtc",
                schema: "guests",
                table: "property_projection",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyContentSha256",
                schema: "guests",
                table: "property_projection",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyEffectiveAtUtc",
                schema: "guests",
                table: "property_projection",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyExpiresAtUtc",
                schema: "guests",
                table: "property_projection",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PolicySourceVersion",
                schema: "guests",
                table: "property_projection",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingStatus",
                schema: "guests",
                table: "property_projection",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RetentionPolicyId",
                schema: "guests",
                table: "property_projection",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionPolicyVersion",
                schema: "guests",
                table: "property_projection",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferProfileId",
                schema: "guests",
                table: "property_projection",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "property_policy_acknowledgements",
                schema: "guests",
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
                        principalSchema: "guests",
                        principalTable: "property_projection",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_guests_property_projection_governance_policy",
                schema: "guests",
                table: "property_projection",
                sql: "(\"ProcessingStatus\" = 1 AND \"OperatingCountryCode\" IS NULL AND \"JurisdictionPolicyId\" IS NULL AND \"JurisdictionPolicyVersion\" IS NULL AND \"DataRegionId\" IS NULL AND \"TransferProfileId\" IS NULL AND \"RetentionPolicyId\" IS NULL AND \"RetentionPolicyVersion\" IS NULL AND \"PolicyContentSha256\" IS NULL AND \"PolicyEffectiveAtUtc\" IS NULL AND \"PolicyExpiresAtUtc\" IS NULL AND \"PolicyActivatedAtUtc\" IS NULL) OR (\"ProcessingStatus\" IN (2, 3) AND \"OperatingCountryCode\" IS NOT NULL AND \"JurisdictionPolicyId\" IS NOT NULL AND \"JurisdictionPolicyVersion\" IS NOT NULL AND \"DataRegionId\" IS NOT NULL AND \"TransferProfileId\" IS NOT NULL AND \"RetentionPolicyId\" IS NOT NULL AND \"RetentionPolicyVersion\" IS NOT NULL AND \"PolicyContentSha256\" IS NOT NULL AND \"PolicyEffectiveAtUtc\" IS NOT NULL AND \"PolicyExpiresAtUtc\" IS NOT NULL AND \"PolicyActivatedAtUtc\" IS NOT NULL AND \"JurisdictionPolicyVersion\" > 0 AND \"RetentionPolicyVersion\" > 0 AND char_length(\"OperatingCountryCode\") = 2 AND char_length(\"PolicyContentSha256\") = 64 AND \"PolicyEffectiveAtUtc\" < \"PolicyExpiresAtUtc\" AND \"PolicyActivatedAtUtc\" >= \"PolicyEffectiveAtUtc\" AND \"PolicyActivatedAtUtc\" < \"PolicyExpiresAtUtc\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_guests_property_projection_processing_status",
                schema: "guests",
                table: "property_projection",
                sql: "\"ProcessingStatus\" BETWEEN 1 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_guests_property_projection_versions",
                schema: "guests",
                table: "property_projection",
                sql: "\"TopologySourceVersion\" >= 0 AND \"PolicySourceVersion\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "property_policy_acknowledgements",
                schema: "guests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_guests_property_projection_governance_policy",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropCheckConstraint(
                name: "CK_guests_property_projection_processing_status",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropCheckConstraint(
                name: "CK_guests_property_projection_versions",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "DataRegionId",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "IsKnown",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "JurisdictionPolicyId",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "JurisdictionPolicyVersion",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "OperatingCountryCode",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicyActivatedAtUtc",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicyContentSha256",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicyEffectiveAtUtc",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicyExpiresAtUtc",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "PolicySourceVersion",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyId",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyVersion",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "TransferProfileId",
                schema: "guests",
                table: "property_projection");

            migrationBuilder.RenameColumn(
                name: "TopologySourceVersion",
                schema: "guests",
                table: "property_projection",
                newName: "Version");

            migrationBuilder.AddCheckConstraint(
                name: "CK_guests_property_projection_version",
                schema: "guests",
                table: "property_projection",
                sql: "\"Version\" >= 1");
        }
    }
}
