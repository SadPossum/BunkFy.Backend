using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDataRightsAnonymisationDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."execution_work_items"
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot enable anonymisation dispatch while legacy execution work items exist. Resolve or remove pre-dispatch executions before retrying the migration.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_state",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAtUtc",
                schema: "data-rights",
                table: "execution_work_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastTaskAttempt",
                schema: "data-rights",
                table: "execution_work_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OutcomeAtUtc",
                schema: "data-rights",
                table: "execution_work_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeCode",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OwnerCompletedAtUtc",
                schema: "data-rights",
                table: "execution_work_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerContractVersion",
                schema: "data-rights",
                table: "execution_work_items",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "OwnerDispositionCode",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerReasonCode",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerReceiptContractVersion",
                schema: "data-rights",
                table: "execution_work_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerReceiptId",
                schema: "data-rights",
                table: "execution_work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerReceiptSha256",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ResultingRecordVersion",
                schema: "data-rights",
                table: "execution_work_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskRunId",
                schema: "data-rights",
                table: "execution_work_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_work_items_ScopeId_TaskRunId",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "TaskRunId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_owner_contract",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "\"OwnerContractVersion\" = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_owner_outcome",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "((\"State\" IN (5, 7)) AND \"OwnerReceiptContractVersion\" >= 1 AND \"OwnerReceiptId\" IS NOT NULL AND \"ResultingRecordVersion\" > \"SelectedRecordVersion\" AND length(trim(\"OwnerDispositionCode\")) > 0 AND length(trim(\"OwnerReasonCode\")) > 0 AND char_length(\"OwnerReceiptSha256\") = 64 AND \"OwnerCompletedAtUtc\" IS NOT NULL AND \"OutcomeCode\" IS NULL AND \"OutcomeAtUtc\" IS NOT NULL AND \"OwnerCompletedAtUtc\" <= \"OutcomeAtUtc\") OR ((\"State\" IN (3, 4, 6)) AND \"OwnerReceiptContractVersion\" IS NULL AND \"OwnerReceiptId\" IS NULL AND \"ResultingRecordVersion\" IS NULL AND \"OwnerDispositionCode\" IS NULL AND \"OwnerReasonCode\" IS NULL AND \"OwnerReceiptSha256\" IS NULL AND \"OwnerCompletedAtUtc\" IS NULL AND length(trim(\"OutcomeCode\")) > 0 AND \"OutcomeAtUtc\" IS NOT NULL) OR ((\"State\" IN (1, 2)) AND \"OwnerReceiptContractVersion\" IS NULL AND \"OwnerReceiptId\" IS NULL AND \"ResultingRecordVersion\" IS NULL AND \"OwnerDispositionCode\" IS NULL AND \"OwnerReasonCode\" IS NULL AND \"OwnerReceiptSha256\" IS NULL AND \"OwnerCompletedAtUtc\" IS NULL AND \"OutcomeCode\" IS NULL AND \"OutcomeAtUtc\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_state",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "\"State\" BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_task",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "(\"State\" = 1 AND \"TaskRunId\" IS NULL AND \"LastTaskAttempt\" = 0 AND \"LastAttemptAtUtc\" IS NULL AND \"AttemptCount\" = 0) OR (\"State\" BETWEEN 2 AND 7 AND \"TaskRunId\" IS NOT NULL AND \"LastTaskAttempt\" >= 1 AND \"LastAttemptAtUtc\" IS NOT NULL AND \"AttemptCount\" >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_timestamps",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "(\"LastAttemptAtUtc\" IS NULL OR \"LastAttemptAtUtc\" >= \"CreatedAtUtc\") AND (\"OwnerCompletedAtUtc\" IS NULL OR \"OwnerCompletedAtUtc\" >= \"CreatedAtUtc\") AND (\"OutcomeAtUtc\" IS NULL OR \"OutcomeAtUtc\" >= \"CreatedAtUtc\")");

            migrationBuilder.Sql(
                """
                ALTER TABLE "data-rights"."execution_work_items"
                    ALTER COLUMN "OwnerContractVersion" DROP DEFAULT;
                """);
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
                        FROM "data-rights"."execution_work_items"
                        WHERE "State" <> 1
                           OR "TaskRunId" IS NOT NULL
                           OR "OwnerReceiptId" IS NOT NULL
                           OR "OutcomeCode" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot remove anonymisation dispatch after owner work has started or durable owner outcomes exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_execution_work_items_ScopeId_TaskRunId",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_owner_contract",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_owner_outcome",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_state",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_task",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_timestamps",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "LastAttemptAtUtc",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "LastTaskAttempt",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "OutcomeAtUtc",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "OutcomeCode",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "OwnerCompletedAtUtc",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "OwnerContractVersion",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "OwnerDispositionCode",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "OwnerReasonCode",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "OwnerReceiptContractVersion",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "OwnerReceiptId",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "OwnerReceiptSha256",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "ResultingRecordVersion",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "TaskRunId",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_state",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "\"State\" BETWEEN 1 AND 6");
        }
    }
}
