using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Properties.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class WidenPropertyLifecycleMutationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"Kind\" IN (1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_properties_property_mutation_operations_kind",
                schema: "properties",
                table: "property_mutation_operations",
                sql: "\"Kind\" = 1");
        }
    }
}
