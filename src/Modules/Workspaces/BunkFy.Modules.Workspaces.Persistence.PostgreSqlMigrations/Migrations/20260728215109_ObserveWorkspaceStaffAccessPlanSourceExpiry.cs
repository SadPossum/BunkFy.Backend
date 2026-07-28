using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Workspaces.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ObserveWorkspaceStaffAccessPlanSourceExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SourceExpiredAtUtc",
                schema: "workspaces",
                table: "staff_access_plans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE workspaces.staff_access_plans
                SET "SourceExpiredAtUtc" = "LastChangedAtUtc"
                WHERE "Status" = 4 AND "SourceExpiredAtUtc" IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_access_plans_expiry_authority",
                schema: "workspaces",
                table: "staff_access_plans",
                sql: "\"Status\" <> 4 OR \"SourceExpiredAtUtc\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_access_plans_expiry_authority",
                schema: "workspaces",
                table: "staff_access_plans");

            migrationBuilder.DropColumn(
                name: "SourceExpiredAtUtc",
                schema: "workspaces",
                table: "staff_access_plans");
        }
    }
}
