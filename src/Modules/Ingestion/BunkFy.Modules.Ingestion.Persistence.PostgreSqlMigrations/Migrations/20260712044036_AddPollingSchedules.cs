using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPollingSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PollingIntervalSeconds",
                schema: "ingestion",
                table: "adapter_connections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PollingScheduleConfiguredAtUtc",
                schema: "ingestion",
                table: "adapter_connections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PollingScheduleMaxAttempts",
                schema: "ingestion",
                table: "adapter_connections",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_runs_ScopeId_ConnectionId",
                schema: "ingestion",
                table: "runs",
                columns: new[] { "ScopeId", "ConnectionId" },
                unique: true,
                filter: "\"State\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_adapter_connections_State_ExecutionMode_PollingIntervalSeco~",
                schema: "ingestion",
                table: "adapter_connections",
                columns: new[] { "State", "ExecutionMode", "PollingIntervalSeconds" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_adapter_connections_polling_schedule_complete",
                schema: "ingestion",
                table: "adapter_connections",
                sql: "(\"PollingIntervalSeconds\" IS NULL AND \"PollingScheduleMaxAttempts\" IS NULL AND \"PollingScheduleConfiguredAtUtc\" IS NULL) OR (\"PollingIntervalSeconds\" BETWEEN 60 AND 2592000 AND \"PollingScheduleMaxAttempts\" BETWEEN 1 AND 10 AND \"PollingScheduleConfiguredAtUtc\" IS NOT NULL AND \"ExecutionMode\" = 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_runs_ScopeId_ConnectionId",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropIndex(
                name: "IX_adapter_connections_State_ExecutionMode_PollingIntervalSeco~",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_adapter_connections_polling_schedule_complete",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "PollingIntervalSeconds",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "PollingScheduleConfiguredAtUtc",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "PollingScheduleMaxAttempts",
                schema: "ingestion",
                table: "adapter_connections");
        }
    }
}
