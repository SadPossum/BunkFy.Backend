using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Properties.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyDetailsUpdateOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_tenant_destroy_operation_progress",
                schema: "properties",
                table: "tenant_destroy_operations");

            migrationBuilder.CreateTable(
                name: "property_mutation_operations",
                schema: "properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultStatus = table.Column<int>(type: "integer", nullable: false),
                    ResultProcessingStatus = table.Column<int>(type: "integer", nullable: false),
                    ResultVersion = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_mutation_operations", x => new { x.ScopeId, x.PropertyId, x.Id });
                    table.CheckConstraint("CK_properties_property_mutation_operations_fingerprint", "char_length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_properties_property_mutation_operations_kind", "\"Kind\" = 1");
                    table.CheckConstraint("CK_properties_property_mutation_operations_processing", "\"ResultProcessingStatus\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_properties_property_mutation_operations_status", "\"ResultStatus\" IN (1, 2)");
                    table.CheckConstraint("CK_properties_property_mutation_operations_versions", "\"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + 1");
                    table.ForeignKey(
                        name: "FK_property_mutation_operations_properties_ScopeId_PropertyId",
                        columns: x => new { x.ScopeId, x.PropertyId },
                        principalSchema: "properties",
                        principalTable: "properties",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_tenant_destroy_operation_progress",
                schema: "properties",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 11 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_property_mutation_operations_ScopeId_CompletedAtUtc_Id",
                schema: "properties",
                table: "property_mutation_operations",
                columns: new[] { "ScopeId", "CompletedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "property_mutation_operations",
                schema: "properties");

            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_tenant_destroy_operation_progress",
                schema: "properties",
                table: "tenant_destroy_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_tenant_destroy_operation_progress",
                schema: "properties",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 10 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
        }
    }
}
