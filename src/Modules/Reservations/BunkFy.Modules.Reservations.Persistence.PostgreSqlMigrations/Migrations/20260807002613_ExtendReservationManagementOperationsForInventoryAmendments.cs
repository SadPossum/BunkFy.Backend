using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ExtendReservationManagementOperationsForInventoryAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_business_date",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_expected_revision",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_kind",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                schema: "reservations",
                table: "management_operations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_business_date",
                schema: "reservations",
                table: "management_operations",
                sql: "(\"Kind\" IN (1, 5, 6) AND \"BusinessDate\" IS NULL) OR (\"Kind\" IN (2, 3, 4) AND \"BusinessDate\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_expected_revision",
                schema: "reservations",
                table: "management_operations",
                sql: "(\"Kind\" IN (1, 2, 3, 4) AND \"ExpectedVersion\" > 0 AND \"ExpectedDetailsRevision\" IS NULL) OR (\"Kind\" IN (5, 6) AND \"ExpectedVersion\" IS NULL AND \"ExpectedDetailsRevision\" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_kind",
                schema: "reservations",
                table: "management_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4, 5, 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_request_fingerprint",
                schema: "reservations",
                table: "management_operations",
                sql: "(\"Kind\" IN (1, 2, 3, 4, 5) AND \"RequestFingerprint\" IS NULL) OR (\"Kind\" = 6 AND \"RequestFingerprint\" IS NOT NULL AND char_length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" ~ '^[0-9a-f]{64}$')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_business_date",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_expected_revision",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_kind",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_request_fingerprint",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_business_date",
                schema: "reservations",
                table: "management_operations",
                sql: "(\"Kind\" IN (1, 5) AND \"BusinessDate\" IS NULL) OR (\"Kind\" IN (2, 3, 4) AND \"BusinessDate\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_expected_revision",
                schema: "reservations",
                table: "management_operations",
                sql: "(\"Kind\" IN (1, 2, 3, 4) AND \"ExpectedVersion\" > 0 AND \"ExpectedDetailsRevision\" IS NULL) OR (\"Kind\" = 5 AND \"ExpectedVersion\" IS NULL AND \"ExpectedDetailsRevision\" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_kind",
                schema: "reservations",
                table: "management_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4, 5)");
        }
    }
}
