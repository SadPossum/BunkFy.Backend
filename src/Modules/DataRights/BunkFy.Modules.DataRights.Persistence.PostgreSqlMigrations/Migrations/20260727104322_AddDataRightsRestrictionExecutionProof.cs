using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDataRightsRestrictionExecutionProof : Migration
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
                        FROM "data-rights"."cases"
                        WHERE "RequestedOperations" = 4
                          AND "Status" = 9)
                    THEN
                        RAISE EXCEPTION
                            'Cannot add restriction execution proof: a legacy completed restriction case has no authoritative owner receipt.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<long>(
                name: "RestrictionExecutionApprovalRevision",
                schema: "data-rights",
                table: "cases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RestrictionExecutionCompletedAtUtc",
                schema: "data-rights",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestrictionExecutionDirective",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RestrictionExecutionEffectiveRestricted",
                schema: "data-rights",
                table: "cases",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictionExecutionExecutedBy",
                schema: "data-rights",
                table: "cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RestrictionExecutionIdempotencyKey",
                schema: "data-rights",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictionExecutionOwnerKey",
                schema: "data-rights",
                table: "cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RestrictionExecutionOwnerOperationId",
                schema: "data-rights",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestrictionExecutionReceiptContractVersion",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RestrictionExecutionReceiptId",
                schema: "data-rights",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictionExecutionReceiptSha256",
                schema: "data-rights",
                table: "cases",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RestrictionExecutionRecordId",
                schema: "data-rights",
                table: "cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictionExecutionRecordType",
                schema: "data-rights",
                table: "cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RestrictionExecutionResultingOwnerRevision",
                schema: "data-rights",
                table: "cases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RestrictionExecutionResultingProjectionRevision",
                schema: "data-rights",
                table: "cases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RestrictionExecutionSelectedRecordVersion",
                schema: "data-rights",
                table: "cases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_restriction_execution_proof",
                schema: "data-rights",
                table: "cases",
                sql: "((\"RequestedOperations\" = 4 AND \"Status\" = 9) AND \"RestrictionExecutionIdempotencyKey\" IS NOT NULL AND \"RestrictionExecutionApprovalRevision\" = \"DecisionRevision\" AND \"RestrictionExecutionDirective\" = \"RestrictionDirective\" AND length(trim(\"RestrictionExecutionOwnerKey\")) > 0 AND length(trim(\"RestrictionExecutionRecordType\")) > 0 AND \"RestrictionExecutionRecordId\" IS NOT NULL AND \"RestrictionExecutionSelectedRecordVersion\" >= 1 AND \"RestrictionExecutionReceiptContractVersion\" >= 1 AND \"RestrictionExecutionReceiptId\" IS NOT NULL AND \"RestrictionExecutionOwnerOperationId\" IS NOT NULL AND \"RestrictionExecutionResultingOwnerRevision\" >= 1 AND \"RestrictionExecutionResultingProjectionRevision\" >= 1 AND ((\"RestrictionExecutionDirective\" = 1 AND \"RestrictionExecutionEffectiveRestricted\" = TRUE) OR (\"RestrictionExecutionDirective\" = 2 AND \"RestrictionExecutionEffectiveRestricted\" = FALSE)) AND \"RestrictionExecutionReceiptSha256\" IS NOT NULL AND char_length(\"RestrictionExecutionReceiptSha256\") = 64 AND length(trim(\"RestrictionExecutionExecutedBy\")) > 0 AND \"RestrictionExecutionCompletedAtUtc\" >= \"DecidedAtUtc\" AND \"RestrictionExecutionCompletedAtUtc\" <= \"LastChangedAtUtc\") OR ((\"RequestedOperations\" <> 4 OR \"Status\" <> 9) AND \"RestrictionExecutionIdempotencyKey\" IS NULL AND \"RestrictionExecutionApprovalRevision\" IS NULL AND \"RestrictionExecutionDirective\" IS NULL AND \"RestrictionExecutionOwnerKey\" IS NULL AND \"RestrictionExecutionRecordType\" IS NULL AND \"RestrictionExecutionRecordId\" IS NULL AND \"RestrictionExecutionSelectedRecordVersion\" IS NULL AND \"RestrictionExecutionReceiptContractVersion\" IS NULL AND \"RestrictionExecutionReceiptId\" IS NULL AND \"RestrictionExecutionOwnerOperationId\" IS NULL AND \"RestrictionExecutionResultingOwnerRevision\" IS NULL AND \"RestrictionExecutionResultingProjectionRevision\" IS NULL AND \"RestrictionExecutionEffectiveRestricted\" IS NULL AND \"RestrictionExecutionReceiptSha256\" IS NULL AND \"RestrictionExecutionExecutedBy\" IS NULL AND \"RestrictionExecutionCompletedAtUtc\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_restriction_execution_proof",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionApprovalRevision",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionCompletedAtUtc",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionDirective",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionEffectiveRestricted",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionExecutedBy",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionIdempotencyKey",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionOwnerKey",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionOwnerOperationId",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionReceiptContractVersion",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionReceiptId",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionReceiptSha256",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionRecordId",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionRecordType",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionResultingOwnerRevision",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionResultingProjectionRevision",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "RestrictionExecutionSelectedRecordVersion",
                schema: "data-rights",
                table: "cases");
        }
    }
}
