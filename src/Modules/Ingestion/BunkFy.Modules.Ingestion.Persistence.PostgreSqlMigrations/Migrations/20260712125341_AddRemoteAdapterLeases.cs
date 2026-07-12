using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteAdapterLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_runs_ScopeId_TaskRunId_TaskAttempt",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.AlterColumn<Guid>(
                name: "TaskRunId",
                schema: "ingestion",
                table: "runs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "TaskAttempt",
                schema: "ingestion",
                table: "runs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ExecutionKind",
                schema: "ingestion",
                table: "runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteClaimId",
                schema: "ingestion",
                table: "runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteCredentialId",
                schema: "ingestion",
                table: "runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RemoteLeaseEpoch",
                schema: "ingestion",
                table: "runs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemoteLeaseExpiresAtUtc",
                schema: "ingestion",
                table: "runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteLeaseId",
                schema: "ingestion",
                table: "runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteWorkerId",
                schema: "ingestion",
                table: "runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE ingestion.runs SET \"ExecutionKind\" = 1 WHERE \"ExecutionKind\" = 0;");

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteLeaseClaimId",
                schema: "ingestion",
                table: "adapter_connections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteLeaseCredentialId",
                schema: "ingestion",
                table: "adapter_connections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RemoteLeaseEpoch",
                schema: "ingestion",
                table: "adapter_connections",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemoteLeaseExpiresAtUtc",
                schema: "ingestion",
                table: "adapter_connections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteLeaseId",
                schema: "ingestion",
                table: "adapter_connections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteLeaseRunId",
                schema: "ingestion",
                table: "adapter_connections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteLeaseWorkerId",
                schema: "ingestion",
                table: "adapter_connections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_runs_ScopeId_RemoteLeaseId",
                schema: "ingestion",
                table: "runs",
                columns: new[] { "ScopeId", "RemoteLeaseId" },
                unique: true,
                filter: "\"ExecutionKind\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_runs_ScopeId_TaskRunId_TaskAttempt",
                schema: "ingestion",
                table: "runs",
                columns: new[] { "ScopeId", "TaskRunId", "TaskAttempt" },
                unique: true,
                filter: "\"ExecutionKind\" = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_runs_execution_identity",
                schema: "ingestion",
                table: "runs",
                sql: "(\"ExecutionKind\" = 1 AND \"TaskRunId\" IS NOT NULL AND \"TaskAttempt\" > 0 AND \"RemoteLeaseId\" IS NULL AND \"RemoteClaimId\" IS NULL AND \"RemoteLeaseEpoch\" IS NULL AND \"RemoteCredentialId\" IS NULL AND \"RemoteWorkerId\" IS NULL AND \"RemoteLeaseExpiresAtUtc\" IS NULL) OR (\"ExecutionKind\" = 2 AND \"TaskRunId\" IS NULL AND \"TaskAttempt\" IS NULL AND \"RemoteLeaseId\" IS NOT NULL AND \"RemoteClaimId\" IS NOT NULL AND \"RemoteLeaseEpoch\" > 0 AND \"RemoteCredentialId\" IS NOT NULL AND \"RemoteWorkerId\" IS NOT NULL AND \"RemoteLeaseExpiresAtUtc\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_adapter_connections_ScopeId_ExecutionMode_RemoteLeaseExpire~",
                schema: "ingestion",
                table: "adapter_connections",
                columns: new[] { "ScopeId", "ExecutionMode", "RemoteLeaseExpiresAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_adapter_connections_remote_lease_complete",
                schema: "ingestion",
                table: "adapter_connections",
                sql: "(\"RemoteLeaseRunId\" IS NULL AND \"RemoteLeaseId\" IS NULL AND \"RemoteLeaseClaimId\" IS NULL AND \"RemoteLeaseCredentialId\" IS NULL AND \"RemoteLeaseWorkerId\" IS NULL AND \"RemoteLeaseExpiresAtUtc\" IS NULL) OR (\"ExecutionMode\" = 4 AND \"RemoteLeaseRunId\" IS NOT NULL AND \"RemoteLeaseId\" IS NOT NULL AND \"RemoteLeaseClaimId\" IS NOT NULL AND \"RemoteLeaseCredentialId\" IS NOT NULL AND \"RemoteLeaseWorkerId\" IS NOT NULL AND \"RemoteLeaseEpoch\" > 0 AND \"RemoteLeaseExpiresAtUtc\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_runs_ScopeId_RemoteLeaseId",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropIndex(
                name: "IX_runs_ScopeId_TaskRunId_TaskAttempt",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_runs_execution_identity",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropIndex(
                name: "IX_adapter_connections_ScopeId_ExecutionMode_RemoteLeaseExpire~",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_adapter_connections_remote_lease_complete",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "ExecutionKind",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "RemoteClaimId",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "RemoteCredentialId",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseEpoch",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseExpiresAtUtc",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseId",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "RemoteWorkerId",
                schema: "ingestion",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseClaimId",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseCredentialId",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseEpoch",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseExpiresAtUtc",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseId",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseRunId",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.DropColumn(
                name: "RemoteLeaseWorkerId",
                schema: "ingestion",
                table: "adapter_connections");

            migrationBuilder.AlterColumn<Guid>(
                name: "TaskRunId",
                schema: "ingestion",
                table: "runs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TaskAttempt",
                schema: "ingestion",
                table: "runs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_runs_ScopeId_TaskRunId_TaskAttempt",
                schema: "ingestion",
                table: "runs",
                columns: new[] { "ScopeId", "TaskRunId", "TaskAttempt" },
                unique: true);
        }
    }
}
