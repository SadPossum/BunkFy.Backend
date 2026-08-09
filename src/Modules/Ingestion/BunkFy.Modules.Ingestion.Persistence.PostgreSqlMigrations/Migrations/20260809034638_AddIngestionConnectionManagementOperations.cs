using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionConnectionManagementOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_tenant_destroy_operation_progress",
                schema: "ingestion",
                table: "tenant_destroy_operations");

            migrationBuilder.Sql(
                """
                UPDATE ingestion.tenant_destroy_operations
                SET "Stage" = "Stage" + 1
                WHERE "Stage" BETWEEN 18 AND 22;
                """);

            migrationBuilder.CreateTable(
                name: "connection_management_operations",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultVersion = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_management_operations", x => new { x.ScopeId, x.ConnectionId, x.Id });
                    table.CheckConstraint("CK_ingestion_connection_management_operations_coordinates", "\"Id\" <> '00000000-0000-0000-0000-000000000000' AND \"PropertyId\" <> '00000000-0000-0000-0000-000000000000' AND \"ConnectionId\" <> '00000000-0000-0000-0000-000000000000' AND char_length(btrim(\"ScopeId\")) > 0");
                    table.CheckConstraint("CK_ingestion_connection_management_operations_create", "\"Kind\" = 1 AND \"ExpectedVersion\" = 0 AND \"ResultVersion\" = 1 AND \"Id\" = \"ConnectionId\"");
                    table.CheckConstraint("CK_ingestion_connection_management_operations_fingerprint", "char_length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_connection_management_operations_adapter_connections_ScopeI~",
                        columns: x => new { x.ScopeId, x.ConnectionId },
                        principalSchema: "ingestion",
                        principalTable: "adapter_connections",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_tenant_destroy_operation_progress",
                schema: "ingestion",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 23 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"RemovedRawPayloadCount\" >= 0 AND \"CompletedRawPayloadBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"RawPayloadRemovalProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_connection_management_operations_ScopeId_PropertyId_Complet~",
                schema: "ingestion",
                table: "connection_management_operations",
                columns: new[] { "ScopeId", "PropertyId", "CompletedAtUtc", "ConnectionId", "Id" });
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
                        FROM ingestion.connection_management_operations)
                    THEN
                        RAISE EXCEPTION 'Cannot remove Ingestion connection management operations while receipts exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "connection_management_operations",
                schema: "ingestion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_tenant_destroy_operation_progress",
                schema: "ingestion",
                table: "tenant_destroy_operations");

            migrationBuilder.Sql(
                """
                UPDATE ingestion.tenant_destroy_operations
                SET "Stage" = CASE
                    WHEN "Stage" = 18 THEN 18
                    ELSE "Stage" - 1
                END
                WHERE "Stage" BETWEEN 18 AND 23;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_tenant_destroy_operation_progress",
                schema: "ingestion",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 22 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"RemovedRawPayloadCount\" >= 0 AND \"CompletedRawPayloadBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"RawPayloadRemovalProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
        }
    }
}
