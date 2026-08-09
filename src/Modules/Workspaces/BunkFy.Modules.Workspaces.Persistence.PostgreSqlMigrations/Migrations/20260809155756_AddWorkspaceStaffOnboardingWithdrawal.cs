using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceStaffOnboardingWithdrawal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_onboarding_pending_profile",
                schema: "workspaces",
                table: "staff_onboarding_applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_onboarding_status",
                schema: "workspaces",
                table: "staff_onboarding_applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_onboarding_terminal_redaction",
                schema: "workspaces",
                table: "staff_onboarding_applications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_onboarding_pending_profile",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                sql: "\"Status\" IN (5, 7, 8, 9, 10) OR (\"VerifiedAccountEmail\" IS NOT NULL AND \"DisplayName\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_onboarding_status",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                sql: "\"Status\" BETWEEN 1 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_onboarding_terminal_redaction",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                sql: "\"Status\" NOT IN (5, 7, 8, 9, 10) OR (\"VerifiedAccountEmail\" IS NULL AND \"DisplayName\" IS NULL AND \"LegalName\" IS NULL AND \"WorkEmail\" IS NULL AND \"WorkPhone\" IS NULL AND \"EmployeeNumber\" IS NULL AND \"JobTitle\" IS NULL AND \"Department\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_onboarding_pending_profile",
                schema: "workspaces",
                table: "staff_onboarding_applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_onboarding_status",
                schema: "workspaces",
                table: "staff_onboarding_applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_onboarding_terminal_redaction",
                schema: "workspaces",
                table: "staff_onboarding_applications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_onboarding_pending_profile",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                sql: "\"Status\" IN (5, 7, 8, 9) OR (\"VerifiedAccountEmail\" IS NOT NULL AND \"DisplayName\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_onboarding_status",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                sql: "\"Status\" BETWEEN 1 AND 9");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_onboarding_terminal_redaction",
                schema: "workspaces",
                table: "staff_onboarding_applications",
                sql: "\"Status\" NOT IN (5, 7, 8, 9) OR (\"VerifiedAccountEmail\" IS NULL AND \"DisplayName\" IS NULL AND \"LegalName\" IS NULL AND \"WorkEmail\" IS NULL AND \"WorkPhone\" IS NULL AND \"EmployeeNumber\" IS NULL AND \"JobTitle\" IS NULL AND \"Department\" IS NULL)");
        }
    }
}
