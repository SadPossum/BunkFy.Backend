using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDataRightsResponseDeadlineAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "response_deadline_alert_dispatches",
                schema: "data-rights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertKind = table.Column<int>(type: "integer", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_response_deadline_alert_dispatches", x => x.Id);
                    table.UniqueConstraint("AK_response_deadline_alert_dispatches_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_response_deadline_alert_dispatches_kind", "\"AlertKind\" IN (1, 2)");
                    table.CheckConstraint("CK_response_deadline_alert_dispatches_timing", "(\"AlertKind\" = 1 AND \"DispatchedAtUtc\" < \"DueAtUtc\") OR (\"AlertKind\" = 2 AND \"DispatchedAtUtc\" >= \"DueAtUtc\")");
                    table.ForeignKey(
                        name: "FK_response_deadline_alert_dispatches_cases_ScopeId_CaseId",
                        columns: x => new { x.ScopeId, x.CaseId },
                        principalSchema: "data-rights",
                        principalTable: "cases",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cases_ScopeId_Kind_RequesterRelationship_DueAtUtc_Id",
                schema: "data-rights",
                table: "cases",
                columns: new[] { "ScopeId", "Kind", "RequesterRelationship", "DueAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_response_deadline_alert_dispatches_ScopeId_AlertKind_Dispat~",
                schema: "data-rights",
                table: "response_deadline_alert_dispatches",
                columns: new[] { "ScopeId", "AlertKind", "DispatchedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_response_deadline_alert_dispatches_ScopeId_CaseId_AlertKind",
                schema: "data-rights",
                table: "response_deadline_alert_dispatches",
                columns: new[] { "ScopeId", "CaseId", "AlertKind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."response_deadline_alert_dispatches"
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade Data Rights while response-deadline alert dispatch receipts exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "response_deadline_alert_dispatches",
                schema: "data-rights");

            migrationBuilder.DropIndex(
                name: "IX_cases_ScopeId_Kind_RequesterRelationship_DueAtUtc_Id",
                schema: "data-rights",
                table: "cases");
        }
    }
}
