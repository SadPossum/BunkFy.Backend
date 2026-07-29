using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceStaffOnboardingRetentionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_staff_access_plans_ScopeId_SourceKind_SourceExpiredAtUtc_Id",
                schema: "workspaces",
                table: "staff_access_plans",
                columns: new[] { "ScopeId", "SourceKind", "SourceExpiredAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_access_plans_ScopeId_SourceKind_SourceExpiredAtUtc_Id",
                schema: "workspaces",
                table: "staff_access_plans");
        }
    }
}
