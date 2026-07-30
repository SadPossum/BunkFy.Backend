using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceDataRightsOwnerDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_applications_ScopeId_StaffMemberId_Id",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                columns: new[] { "ScopeId", "StaffMemberId", "Id" },
                filter: "\"StaffMemberId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_onboarding_applications_ScopeId_StaffMemberId_Id",
                schema: "workspaces",
                table: "staff_onboarding_applications");
        }
    }
}
