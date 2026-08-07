using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffProfileUpdateOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_tenant_destroy_operation_progress",
                schema: "staff",
                table: "tenant_destroy_operations");

            migrationBuilder.CreateTable(
                name: "profile_update_operations",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "bigint", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultStatus = table.Column<int>(type: "integer", nullable: false),
                    ResultVersion = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_update_operations", x => new { x.ScopeId, x.StaffMemberId, x.Id });
                    table.CheckConstraint("CK_staff_profile_update_operations_fingerprint", "char_length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_staff_profile_update_operations_status", "\"ResultStatus\" IN (1, 2)");
                    table.CheckConstraint("CK_staff_profile_update_operations_versions", "\"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + 1");
                    table.ForeignKey(
                        name: "FK_profile_update_operations_staff_members_ScopeId_StaffMember~",
                        columns: x => new { x.ScopeId, x.StaffMemberId },
                        principalSchema: "staff",
                        principalTable: "staff_members",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_tenant_destroy_operation_progress",
                schema: "staff",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 23 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_profile_update_operations_ScopeId_CompletedAtUtc_Id",
                schema: "staff",
                table: "profile_update_operations",
                columns: new[] { "ScopeId", "CompletedAtUtc", "Id" });

            migrationBuilder.Sql(
                """
                CREATE FUNCTION "staff".prevent_profile_update_operation_update()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'Staff profile-update operations are immutable';
                END;
                $function$;

                CREATE TRIGGER "TR_staff_profile_update_operations_immutable"
                BEFORE UPDATE ON "staff"."profile_update_operations"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_profile_update_operation_update();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_staff_profile_update_operations_immutable"
                    ON "staff"."profile_update_operations";
                DROP FUNCTION IF EXISTS
                    "staff".prevent_profile_update_operation_update();
                """);

            migrationBuilder.DropTable(
                name: "profile_update_operations",
                schema: "staff");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_tenant_destroy_operation_progress",
                schema: "staff",
                table: "tenant_destroy_operations");

            migrationBuilder.Sql(
                """
                UPDATE staff.tenant_destroy_operations
                SET "Stage" = 21
                WHERE "Stage" = 23;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_tenant_destroy_operation_progress",
                schema: "staff",
                table: "tenant_destroy_operations",
                sql: "\"Stage\" BETWEEN 1 AND 22 AND \"RemovedRecordCount\" >= 0 AND \"CompletedBatchCount\" >= 0 AND \"ProofVersion\" = 1 AND \"ConcurrencyVersion\" >= 1");
        }
    }
}
