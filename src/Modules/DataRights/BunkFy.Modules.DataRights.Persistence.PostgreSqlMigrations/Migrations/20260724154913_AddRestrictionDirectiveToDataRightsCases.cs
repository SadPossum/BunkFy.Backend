using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRestrictionDirectiveToDataRightsCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RestrictionDirective",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_restriction_directive",
                schema: "data-rights",
                table: "cases",
                sql: "((\"RequestedOperations\" & 4) = 0 AND \"RestrictionDirective\" = 0) OR ((\"RequestedOperations\" & 4) = 4 AND \"RestrictionDirective\" BETWEEN 0 AND 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_restriction_directive",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionDirective",
                schema: "data-rights",
                table: "cases");
        }
    }
}
