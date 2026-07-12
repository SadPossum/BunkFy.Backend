using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSensitiveHistoryRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_change_proposals_ScopeId_ConnectionId",
                schema: "ingestion",
                table: "change_proposals");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedSnapshot",
                schema: "ingestion",
                table: "reservation_dispatches",
                type: "character varying(16384)",
                maxLength: 16384,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16384)",
                oldMaxLength: 16384);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SensitiveDataRedactedAtUtc",
                schema: "ingestion",
                table: "reservation_dispatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SensitiveDataRetainUntilUtc",
                schema: "ingestion",
                table: "reservation_dispatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Diff",
                schema: "ingestion",
                table: "change_proposals",
                type: "character varying(32768)",
                maxLength: 32768,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32768)",
                oldMaxLength: 32768);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "ingestion",
                table: "change_proposals",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SensitiveDataRedactedAtUtc",
                schema: "ingestion",
                table: "change_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SensitiveDataRetainUntilUtc",
                schema: "ingestion",
                table: "change_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $migration$
                DECLARE
                    proposal_row record;
                    proposal_diff jsonb;
                    recovered_reason text;
                BEGIN
                    FOR proposal_row IN
                        SELECT "Id", "Diff"
                        FROM ingestion.change_proposals
                    LOOP
                        recovered_reason := 'legacy-unknown';
                        BEGIN
                            proposal_diff := proposal_row."Diff"::jsonb;
                            recovered_reason := lower(trim(COALESCE(
                                proposal_diff ->> 'Reason',
                                proposal_diff ->> 'reason',
                                recovered_reason)));
                            IF length(recovered_reason) = 0 OR length(recovered_reason) > 100 THEN
                                recovered_reason := 'legacy-unknown';
                            END IF;
                        EXCEPTION WHEN OTHERS THEN
                            recovered_reason := 'legacy-unknown';
                        END;

                        UPDATE ingestion.change_proposals
                        SET "ReasonCode" = recovered_reason
                        WHERE "Id" = proposal_row."Id";
                    END LOOP;
                END
                $migration$;

                UPDATE ingestion.change_proposals
                SET "SensitiveDataRetainUntilUtc" =
                    COALESCE("CompletedAtUtc", "DecidedAtUtc", "CreatedAtUtc") + INTERVAL '90 days'
                WHERE "State" IN (3, 4, 5, 6, 7);

                UPDATE ingestion.reservation_dispatches
                SET "SensitiveDataRetainUntilUtc" =
                    COALESCE("CompletedAtUtc", "CreatedAtUtc") + INTERVAL '90 days'
                WHERE "State" IN (3, 4, 5, 6, 7);

                ALTER TABLE ingestion.change_proposals
                    ALTER COLUMN "ReasonCode" DROP DEFAULT;

                ALTER TABLE ingestion.change_proposals
                    ADD CONSTRAINT "CK_change_proposals_sensitive_history_lifecycle" CHECK (
                        ("State" IN (1, 2) AND "Diff" IS NOT NULL AND
                         "SensitiveDataRetainUntilUtc" IS NULL AND "SensitiveDataRedactedAtUtc" IS NULL)
                        OR
                        ("State" IN (3, 4, 5, 6, 7) AND "SensitiveDataRetainUntilUtc" IS NOT NULL AND
                         "SensitiveDataRetainUntilUtc" > COALESCE("CompletedAtUtc", "DecidedAtUtc", "CreatedAtUtc") AND
                         (("Diff" IS NOT NULL AND "SensitiveDataRedactedAtUtc" IS NULL)
                          OR
                          ("Diff" IS NULL AND "SensitiveDataRedactedAtUtc" IS NOT NULL AND
                           "SensitiveDataRedactedAtUtc" >= "SensitiveDataRetainUntilUtc")))
                    );

                ALTER TABLE ingestion.change_proposals
                    ADD CONSTRAINT "CK_change_proposals_reason_code" CHECK (length(trim("ReasonCode")) > 0);

                ALTER TABLE ingestion.reservation_dispatches
                    ADD CONSTRAINT "CK_reservation_dispatches_sensitive_history_lifecycle" CHECK (
                        ("State" IN (1, 2) AND "NormalizedSnapshot" IS NOT NULL AND
                         "SensitiveDataRetainUntilUtc" IS NULL AND "SensitiveDataRedactedAtUtc" IS NULL)
                        OR
                        ("State" IN (3, 4, 5, 6, 7) AND "SensitiveDataRetainUntilUtc" IS NOT NULL AND
                         "SensitiveDataRetainUntilUtc" > COALESCE("CompletedAtUtc", "CreatedAtUtc") AND
                         (("NormalizedSnapshot" IS NOT NULL AND "SensitiveDataRedactedAtUtc" IS NULL)
                          OR
                          ("NormalizedSnapshot" IS NULL AND "SensitiveDataRedactedAtUtc" IS NOT NULL AND
                           "SensitiveDataRedactedAtUtc" >= "SensitiveDataRetainUntilUtc")))
                    );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_dispatches_ScopeId_ConnectionId_SensitiveDataRe~",
                schema: "ingestion",
                table: "reservation_dispatches",
                columns: new[] { "ScopeId", "ConnectionId", "SensitiveDataRetainUntilUtc", "SensitiveDataRedactedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_change_proposals_ScopeId_ConnectionId_SensitiveDataRetainUn~",
                schema: "ingestion",
                table: "change_proposals",
                columns: new[] { "ScopeId", "ConnectionId", "SensitiveDataRetainUntilUtc", "SensitiveDataRedactedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE ingestion.change_proposals
                    DROP CONSTRAINT "CK_change_proposals_sensitive_history_lifecycle";
                ALTER TABLE ingestion.change_proposals
                    DROP CONSTRAINT "CK_change_proposals_reason_code";
                ALTER TABLE ingestion.reservation_dispatches
                    DROP CONSTRAINT "CK_reservation_dispatches_sensitive_history_lifecycle";

                UPDATE ingestion.change_proposals
                SET "Diff" = '{"redacted":true}'
                WHERE "Diff" IS NULL;

                UPDATE ingestion.reservation_dispatches
                SET "NormalizedSnapshot" = '{"redacted":true}'
                WHERE "NormalizedSnapshot" IS NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_reservation_dispatches_ScopeId_ConnectionId_SensitiveDataRe~",
                schema: "ingestion",
                table: "reservation_dispatches");

            migrationBuilder.DropIndex(
                name: "IX_change_proposals_ScopeId_ConnectionId_SensitiveDataRetainUn~",
                schema: "ingestion",
                table: "change_proposals");

            migrationBuilder.DropColumn(
                name: "SensitiveDataRedactedAtUtc",
                schema: "ingestion",
                table: "reservation_dispatches");

            migrationBuilder.DropColumn(
                name: "SensitiveDataRetainUntilUtc",
                schema: "ingestion",
                table: "reservation_dispatches");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "ingestion",
                table: "change_proposals");

            migrationBuilder.DropColumn(
                name: "SensitiveDataRedactedAtUtc",
                schema: "ingestion",
                table: "change_proposals");

            migrationBuilder.DropColumn(
                name: "SensitiveDataRetainUntilUtc",
                schema: "ingestion",
                table: "change_proposals");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedSnapshot",
                schema: "ingestion",
                table: "reservation_dispatches",
                type: "character varying(16384)",
                maxLength: 16384,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16384)",
                oldMaxLength: 16384,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Diff",
                schema: "ingestion",
                table: "change_proposals",
                type: "character varying(32768)",
                maxLength: 32768,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32768)",
                oldMaxLength: 32768,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_change_proposals_ScopeId_ConnectionId",
                schema: "ingestion",
                table: "change_proposals",
                columns: new[] { "ScopeId", "ConnectionId" });
        }
    }
}
