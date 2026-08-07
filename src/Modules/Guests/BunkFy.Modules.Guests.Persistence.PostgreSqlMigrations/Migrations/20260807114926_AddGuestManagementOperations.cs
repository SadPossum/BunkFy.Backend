using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestManagementOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_guests_tenant_destroy_operation_progress",
                schema: "guests",
                table: "tenant_destroy_operations");

            migrationBuilder.CreateTable(
                name: "management_operations",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResultStatus = table.Column<int>(type: "integer", nullable: false),
                    ResultVersion = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_operations", x => new { x.ScopeId, x.GuestId, x.Id });
                    table.CheckConstraint("CK_guest_management_operations_kind", "\"Kind\" IN (1, 2)");
                    table.CheckConstraint("CK_guest_management_operations_result", "(\"Kind\" = 1 AND \"ResultStatus\" = 1 AND \"RequestFingerprint\" IS NOT NULL AND char_length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" ~ '^[0-9a-f]{64}$') OR (\"Kind\" = 2 AND \"ResultStatus\" = 2 AND \"RequestFingerprint\" IS NULL)");
                    table.CheckConstraint("CK_guest_management_operations_versions", "\"ExpectedVersion\" > 0 AND \"ResultVersion\" = \"ExpectedVersion\" + 1");
                    table.ForeignKey(
                        name: "FK_management_operations_guest_profiles_ScopeId_GuestId",
                        columns: x => new { x.ScopeId, x.GuestId },
                        principalSchema: "guests",
                        principalTable: "guest_profiles",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_guests_tenant_destroy_operation_progress",
                schema: "guests",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 21 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_management_operations_ScopeId_PropertyId_CompletedAtUtc_Id",
                schema: "guests",
                table: "management_operations",
                columns: new[] { "ScopeId", "PropertyId", "CompletedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "management_operations",
                schema: "guests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_guests_tenant_destroy_operation_progress",
                schema: "guests",
                table: "tenant_destroy_operations");

            migrationBuilder.Sql(
                """
                UPDATE guests.tenant_destroy_operations
                SET "Stage" = 14
                WHERE "Stage" = 21;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_guests_tenant_destroy_operation_progress",
                schema: "guests",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 20 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
        }
    }
}
