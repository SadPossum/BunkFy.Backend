using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class DropIngestionSourceOperationLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "source_operation_locks",
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
                WHERE "Stage" >= 18;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_tenant_destroy_operation_progress",
                schema: "ingestion",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 22 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"RemovedRawPayloadCount\" >= 0 AND \"CompletedRawPayloadBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"RawPayloadRemovalProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ingestion_tenant_destroy_operation_progress",
                schema: "ingestion",
                table: "tenant_destroy_operations");

            migrationBuilder.CreateTable(
                name: "source_operation_locks",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceLinkId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_operation_locks", x => x.Id);
                    table.UniqueConstraint("AK_source_operation_locks_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_ingestion_source_operation_locks_revision", "\"Revision\" >= 1");
                });

            migrationBuilder.Sql(
                """
                UPDATE ingestion.tenant_destroy_operations
                SET "Stage" = "Stage" + 1
                WHERE "Stage" >= 18;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ingestion_tenant_destroy_operation_progress",
                schema: "ingestion",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 23 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"RemovedRawPayloadCount\" >= 0 AND \"CompletedRawPayloadBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"RawPayloadRemovalProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_source_operation_locks_ScopeId_SourceLinkId",
                schema: "ingestion",
                table: "source_operation_locks",
                columns: new[] { "ScopeId", "SourceLinkId" },
                unique: true);
        }
    }
}
