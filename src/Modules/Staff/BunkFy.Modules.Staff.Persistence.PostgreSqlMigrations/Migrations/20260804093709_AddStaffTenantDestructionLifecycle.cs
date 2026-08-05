using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffTenantDestructionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DestroyCompletedAtUtc",
                schema: "staff",
                table: "tenant_revisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DestroyOperationId",
                schema: "staff",
                table: "tenant_revisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestroyRequestSha256",
                schema: "staff",
                table: "tenant_revisions",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DestroyStartedAtUtc",
                schema: "staff",
                table: "tenant_revisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleStatus",
                schema: "staff",
                table: "tenant_revisions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "tenant_destroy_operations",
                schema: "staff",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SelectedRevision = table.Column<long>(type: "bigint", nullable: false),
                    ResultingRevision = table.Column<long>(type: "bigint", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    RemovedRecordCount = table.Column<long>(type: "bigint", nullable: false),
                    CompletedBatchCount = table.Column<int>(type: "integer", nullable: false),
                    ProofVersion = table.Column<int>(type: "integer", nullable: false),
                    RemovalProofSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_destroy_operations", x => x.OperationId);
                    table.CheckConstraint("CK_staff_tenant_destroy_operation_batch", "\"BatchSize\" BETWEEN 1 AND 500");
                    table.CheckConstraint("CK_staff_tenant_destroy_operation_progress", "\"Stage\" BETWEEN 1 AND 22 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
                    table.CheckConstraint("CK_staff_tenant_destroy_operation_revisions", "\"SelectedRevision\" >= 0 AND \"ResultingRevision\" = \"SelectedRevision\" + 1");
                    table.CheckConstraint("CK_staff_tenant_destroy_operation_times", "\"UpdatedAtUtc\" >= \"StartedAtUtc\"");
                });

            migrationBuilder.CreateTable(
                name: "tenant_destroy_receipts",
                schema: "staff",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SelectedRevision = table.Column<long>(type: "bigint", nullable: false),
                    ResultingRevision = table.Column<long>(type: "bigint", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    RemovedRecordCount = table.Column<long>(type: "bigint", nullable: false),
                    CompletedBatchCount = table.Column<int>(type: "integer", nullable: false),
                    RemovalProofVersion = table.Column<int>(type: "integer", nullable: false),
                    RemovalProofSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_destroy_receipts", x => x.OperationId);
                    table.CheckConstraint("CK_staff_tenant_destroy_receipt_progress", "((\"RemovedRecordCount\" = 0 AND \"CompletedBatchCount\" = 0) OR (\"RemovedRecordCount\" > 0 AND \"CompletedBatchCount\" > 0)) AND \"BatchSize\" BETWEEN 1 AND 500 AND \"RemovalProofVersion\" = 1");
                    table.CheckConstraint("CK_staff_tenant_destroy_receipt_revisions", "\"SelectedRevision\" >= 0 AND \"ResultingRevision\" = \"SelectedRevision\" + 1");
                    table.CheckConstraint("CK_staff_tenant_destroy_receipt_times", "\"CompletedAtUtc\" >= \"StartedAtUtc\"");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_tenant_revision_lifecycle",
                schema: "staff",
                table: "tenant_revisions",
                sql: "(\"LifecycleStatus\" = 1 AND \"DestroyOperationId\" IS NULL AND \"DestroyRequestSha256\" IS NULL AND \"DestroyStartedAtUtc\" IS NULL AND \"DestroyCompletedAtUtc\" IS NULL) OR (\"LifecycleStatus\" = 2 AND \"DestroyOperationId\" IS NOT NULL AND \"DestroyRequestSha256\" IS NOT NULL AND \"DestroyStartedAtUtc\" IS NOT NULL AND \"DestroyCompletedAtUtc\" IS NULL) OR (\"LifecycleStatus\" = 3 AND \"DestroyOperationId\" IS NOT NULL AND \"DestroyRequestSha256\" IS NOT NULL AND \"DestroyStartedAtUtc\" IS NOT NULL AND \"DestroyCompletedAtUtc\" >= \"DestroyStartedAtUtc\")");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_destroy_operations_ScopeId",
                schema: "staff",
                table: "tenant_destroy_operations",
                column: "ScopeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_destroy_receipts_ScopeId",
                schema: "staff",
                table: "tenant_destroy_receipts",
                column: "ScopeId",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION
                    "staff".prevent_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    destroy_operation_id text;
                BEGIN
                    destroy_operation_id := current_setting(
                        'bunkfy.staff_tenant_destroy_operation_id',
                        true);
                    IF TG_OP = 'DELETE' AND
                       destroy_operation_id IS NOT NULL AND
                       EXISTS (
                           SELECT 1
                           FROM "staff".tenant_destroy_operations operation
                           INNER JOIN "staff".tenant_revisions state
                               ON state."ScopeId" = operation."ScopeId"
                           WHERE operation."OperationId"::text =
                                     destroy_operation_id
                             AND operation."ScopeId" = OLD."ScopeId"
                             AND state."LifecycleStatus" = 2
                             AND state."DestroyOperationId" =
                                     operation."OperationId"
                             AND state."DestroyRequestSha256" =
                                     operation."RequestSha256")
                    THEN
                        RETURN OLD;
                    END IF;

                    RAISE EXCEPTION 'Staff receipts are append-only';
                END;
                $function$;

                CREATE OR REPLACE FUNCTION
                    "staff".prevent_tombstone_deletion()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    destroy_operation_id text;
                BEGIN
                    destroy_operation_id := current_setting(
                        'bunkfy.staff_tenant_destroy_operation_id',
                        true);
                    IF destroy_operation_id IS NOT NULL AND
                       EXISTS (
                           SELECT 1
                           FROM "staff".tenant_destroy_operations operation
                           INNER JOIN "staff".tenant_revisions state
                               ON state."ScopeId" = operation."ScopeId"
                           WHERE operation."OperationId"::text =
                                     destroy_operation_id
                             AND operation."ScopeId" = OLD."ScopeId"
                             AND state."LifecycleStatus" = 2
                             AND state."DestroyOperationId" =
                                     operation."OperationId"
                             AND state."DestroyRequestSha256" =
                                     operation."RequestSha256")
                    THEN
                        RETURN OLD;
                    END IF;

                    RAISE EXCEPTION
                        'Staff anonymisation tombstones cannot be deleted';
                END;
                $function$;

                CREATE TRIGGER
                    "TR_staff_tenant_destroy_receipts_append_only"
                BEFORE UPDATE OR DELETE
                ON "staff"."tenant_destroy_receipts"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_receipt_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_staff_tenant_destroy_receipts_append_only"
                ON "staff"."tenant_destroy_receipts";

                CREATE OR REPLACE FUNCTION
                    "staff".prevent_receipt_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'Staff receipts are append-only';
                END;
                $function$;

                CREATE OR REPLACE FUNCTION
                    "staff".prevent_tombstone_deletion()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION
                        'Staff anonymisation tombstones cannot be deleted';
                END;
                $function$;
                """);

            migrationBuilder.DropTable(
                name: "tenant_destroy_operations",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "tenant_destroy_receipts",
                schema: "staff");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_tenant_revision_lifecycle",
                schema: "staff",
                table: "tenant_revisions");

            migrationBuilder.DropColumn(
                name: "DestroyCompletedAtUtc",
                schema: "staff",
                table: "tenant_revisions");

            migrationBuilder.DropColumn(
                name: "DestroyOperationId",
                schema: "staff",
                table: "tenant_revisions");

            migrationBuilder.DropColumn(
                name: "DestroyRequestSha256",
                schema: "staff",
                table: "tenant_revisions");

            migrationBuilder.DropColumn(
                name: "DestroyStartedAtUtc",
                schema: "staff",
                table: "tenant_revisions");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                schema: "staff",
                table: "tenant_revisions");
        }
    }
}
