using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAdapterIngressProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdapterConfigurationSchemaVersion",
                schema: "ingestion",
                table: "observation_receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdapterCredentialId",
                schema: "ingestion",
                table: "observation_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdapterCustomerOwner",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdapterProtocolVersion",
                schema: "ingestion",
                table: "observation_receipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdapterSourceSystem",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdapterType",
                schema: "ingestion",
                table: "observation_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdapterProtocolVersion",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdapterType",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfigurationSchemaVersion",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSystem",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE ingestion.adapter_ingress_credentials AS credential
                SET
                    "AdapterType" = connection."AdapterType",
                    "AdapterProtocolVersion" = CASE connection."AdapterType"
                        WHEN 'fake.http' THEN 1
                        WHEN 'json.file-drop' THEN 1
                        WHEN 'imap.reservation-json' THEN 1
                        ELSE NULL
                    END,
                    "ConfigurationSchemaVersion" = CASE connection."AdapterType"
                        WHEN 'fake.http' THEN 1
                        WHEN 'json.file-drop' THEN 1
                        WHEN 'imap.reservation-json' THEN 3
                        ELSE NULL
                    END,
                    "SourceSystem" = connection."AdapterType"
                FROM ingestion.adapter_connections AS connection
                WHERE connection."Id" = credential."ConnectionId"
                  AND connection."ScopeId" = credential."ScopeId";

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM ingestion.adapter_ingress_credentials
                        WHERE "AdapterType" IS NULL
                           OR "AdapterProtocolVersion" IS NULL
                           OR "ConfigurationSchemaVersion" IS NULL
                           OR "SourceSystem" IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot backfill adapter ingress credential provenance for an unknown or orphaned adapter connection.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "AdapterProtocolVersion",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AdapterType",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ConfigurationSchemaVersion",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SourceSystem",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_observation_receipts_adapter_provenance",
                schema: "ingestion",
                table: "observation_receipts",
                sql: "(\"AdapterType\" IS NULL AND \"AdapterProtocolVersion\" IS NULL AND \"AdapterConfigurationSchemaVersion\" IS NULL AND \"AdapterSourceSystem\" IS NULL AND \"AdapterCredentialId\" IS NULL AND \"AdapterCustomerOwner\" IS NULL) OR (\"AdapterType\" IS NOT NULL AND \"AdapterProtocolVersion\" > 0 AND \"AdapterConfigurationSchemaVersion\" > 0 AND \"AdapterSourceSystem\" IS NOT NULL AND (\"AdapterCredentialId\" IS NULL OR \"AdapterCustomerOwner\" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_adapter_ingress_credentials_ScopeId_SourceSystem_CreatedAtU~",
                schema: "ingestion",
                table: "adapter_ingress_credentials",
                columns: new[] { "ScopeId", "SourceSystem", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_observation_receipts_adapter_provenance",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropIndex(
                name: "IX_adapter_ingress_credentials_ScopeId_SourceSystem_CreatedAtU~",
                schema: "ingestion",
                table: "adapter_ingress_credentials");

            migrationBuilder.DropColumn(
                name: "AdapterConfigurationSchemaVersion",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "AdapterCredentialId",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "AdapterCustomerOwner",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "AdapterProtocolVersion",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "AdapterSourceSystem",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "AdapterType",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "AdapterProtocolVersion",
                schema: "ingestion",
                table: "adapter_ingress_credentials");

            migrationBuilder.DropColumn(
                name: "AdapterType",
                schema: "ingestion",
                table: "adapter_ingress_credentials");

            migrationBuilder.DropColumn(
                name: "ConfigurationSchemaVersion",
                schema: "ingestion",
                table: "adapter_ingress_credentials");

            migrationBuilder.DropColumn(
                name: "SourceSystem",
                schema: "ingestion",
                table: "adapter_ingress_credentials");
        }
    }
}
