using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffLifecycleMutationOperations : Migration
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

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_status",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"ResultStatus\" IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_status",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"Kind\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_status",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"ResultStatus\" IN (1, 2)");
        }
    }
}
