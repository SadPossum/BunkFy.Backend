using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ExtendReservationManagementOperationsForGuestDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_business_date",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_expected_version",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_management_operations_kind",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.AlterColumn<long>(
                name: "ExpectedVersion",
                schema: "reservations",
                table: "management_operations",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "ExpectedDetailsRevision",
                schema: "reservations",
                table: "management_operations",
                type: "bigint",
                nullable: true);

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

            migrationBuilder.DropColumn(
                name: "ExpectedDetailsRevision",
                schema: "reservations",
                table: "management_operations");

            migrationBuilder.AlterColumn<long>(
                name: "ExpectedVersion",
                schema: "reservations",
                table: "management_operations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_business_date",
                schema: "reservations",
                table: "management_operations",
                sql: "(\"Kind\" = 1 AND \"BusinessDate\" IS NULL) OR (\"Kind\" IN (2, 3, 4) AND \"BusinessDate\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_expected_version",
                schema: "reservations",
                table: "management_operations",
                sql: "\"ExpectedVersion\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_management_operations_kind",
                schema: "reservations",
                table: "management_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4)");
        }
    }
}
