using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRawPayloadRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RawPayloadPurgeClaimId",
                schema: "ingestion",
                table: "observation_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RawPayloadPurgeStartedAtUtc",
                schema: "ingestion",
                table: "observation_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RawPayloadPurgedAtUtc",
                schema: "ingestion",
                table: "observation_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RawPayloadRetainUntilUtc",
                schema: "ingestion",
                table: "observation_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RawPayloadRetentionState",
                schema: "ingestion",
                table: "observation_receipts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "RawPayloadVersion",
                schema: "ingestion",
                table: "observation_receipts",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.Sql(
                """
                UPDATE ingestion.observation_receipts
                SET "RawPayloadRetainUntilUtc" = "ReceivedAtUtc" + INTERVAL '30 days';
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RawPayloadRetainUntilUtc",
                schema: "ingestion",
                table: "observation_receipts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RawPayloadRetentionState",
                schema: "ingestion",
                table: "observation_receipts",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<long>(
                name: "RawPayloadVersion",
                schema: "ingestion",
                table: "observation_receipts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 1L);

            migrationBuilder.CreateIndex(
                name: "IX_observation_receipts_ScopeId_RawPayloadRetentionState_RawPa~",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "RawPayloadRetentionState", "RawPayloadRetainUntilUtc", "RawPayloadPurgeStartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_observation_receipts_ScopeId_RawPayloadRetentionState_RawPa~",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "RawPayloadPurgeClaimId",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "RawPayloadPurgeStartedAtUtc",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "RawPayloadPurgedAtUtc",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "RawPayloadRetainUntilUtc",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "RawPayloadRetentionState",
                schema: "ingestion",
                table: "observation_receipts");

            migrationBuilder.DropColumn(
                name: "RawPayloadVersion",
                schema: "ingestion",
                table: "observation_receipts");
        }
    }
}
