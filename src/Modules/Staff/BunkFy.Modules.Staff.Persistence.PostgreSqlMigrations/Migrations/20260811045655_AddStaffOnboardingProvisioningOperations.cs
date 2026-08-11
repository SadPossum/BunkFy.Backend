using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffOnboardingProvisioningOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_status",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_versions",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.CreateIndex(
                name: "UX_staff_member_mutation_operations_onboarding_operation",
                schema: "staff",
                table: "member_mutation_operations",
                columns: new[] { "ScopeId", "Id" },
                unique: true,
                filter: "\"Kind\" = 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4, 5, 6, 7, 8)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_status",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"ResultStatus\" IN (1, 2, 3) AND (\"Kind\" <> 8 OR \"ResultStatus\" = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_versions",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + CASE WHEN \"Kind\" = 8 THEN 2 ELSE 1 END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_staff_member_mutation_operations_onboarding_operation",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_status",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_versions",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4, 5, 6, 7)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_status",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"ResultStatus\" IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_versions",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + 1");
        }
    }
}
