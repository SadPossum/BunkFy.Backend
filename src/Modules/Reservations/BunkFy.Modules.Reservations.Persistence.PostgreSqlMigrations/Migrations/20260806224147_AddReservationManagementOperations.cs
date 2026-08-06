using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationManagementOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_tenant_destroy_operation_progress",
                schema: "reservations",
                table: "tenant_destroy_operations");

            migrationBuilder.CreateTable(
                name: "management_operations",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_operations", x => new { x.ScopeId, x.ReservationId, x.Id });
                    table.CheckConstraint("CK_management_operations_business_date", "(\"Kind\" = 1 AND \"BusinessDate\" IS NULL) OR (\"Kind\" IN (2, 3, 4) AND \"BusinessDate\" IS NOT NULL)");
                    table.CheckConstraint("CK_management_operations_expected_version", "\"ExpectedVersion\" > 0");
                    table.CheckConstraint("CK_management_operations_kind", "\"Kind\" IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_management_operations_reservations_ScopeId_ReservationId",
                        columns: x => new { x.ScopeId, x.ReservationId },
                        principalSchema: "reservations",
                        principalTable: "reservations",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_tenant_destroy_operation_progress",
                schema: "reservations",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 31 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_management_operations_ScopeId_PropertyId_CreatedAtUtc_Id",
                schema: "reservations",
                table: "management_operations",
                columns: new[] { "ScopeId", "PropertyId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "management_operations",
                schema: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reservations_tenant_destroy_operation_progress",
                schema: "reservations",
                table: "tenant_destroy_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_reservations_tenant_destroy_operation_progress",
                schema: "reservations",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 30 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
        }
    }
}
