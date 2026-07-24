using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAtUtc",
                schema: "data-rights",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecidedBy",
                schema: "data-rights",
                table: "cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Decision",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DecisionReason",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "DecisionRevision",
                schema: "data-rights",
                table: "cases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_decision",
                schema: "data-rights",
                table: "cases",
                sql: "\"Decision\" BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_decision_attribution",
                schema: "data-rights",
                table: "cases",
                sql: "\"DecidedBy\" IS NULL OR (length(trim(\"DecidedBy\")) > 0 AND \"DecidedAtUtc\" >= \"CreatedAtUtc\" AND \"DecidedAtUtc\" <= \"LastChangedAtUtc\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_decision_details",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Decision\" = 0 AND \"DecisionRevision\" IS NULL AND \"DecidedBy\" IS NULL AND \"DecidedAtUtc\" IS NULL) OR (\"Decision\" IN (1, 2) AND \"DecisionRevision\" IS NOT NULL AND \"DecisionRevision\" >= 1 AND \"DecisionRevision\" <= \"Version\" AND \"DecidedBy\" IS NOT NULL AND \"DecidedAtUtc\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_decision_reason",
                schema: "data-rights",
                table: "cases",
                sql: "\"DecisionReason\" BETWEEN 0 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_decision_reason_match",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Decision\" = 0 AND \"DecisionReason\" = 0) OR (\"Decision\" = 1 AND \"DecisionReason\" = 1) OR (\"Decision\" = 2 AND \"DecisionReason\" BETWEEN 2 AND 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_decision_state",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Decision\" = 0 AND \"Status\" IN (1, 2, 3, 4, 11)) OR (\"Decision\" = 1 AND \"Status\" IN (5, 7, 8, 9, 10)) OR (\"Decision\" = 2 AND \"Status\" = 6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_decision",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_decision_attribution",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_decision_details",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_decision_reason",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_decision_reason_match",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_decision_state",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "DecidedBy",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "Decision",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "DecisionRevision",
                schema: "data-rights",
                table: "cases");
        }
    }
}
