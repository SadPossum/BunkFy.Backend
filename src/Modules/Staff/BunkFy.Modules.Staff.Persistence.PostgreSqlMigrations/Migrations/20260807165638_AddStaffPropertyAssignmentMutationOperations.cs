using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffPropertyAssignmentMutationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4, 5, 6, 7)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4, 5)");
        }
    }
}
