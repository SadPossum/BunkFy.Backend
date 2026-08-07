using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeStaffMemberMutationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_staff_profile_update_operations_immutable"
                    ON "staff"."profile_update_operations";
                DROP FUNCTION IF EXISTS
                    "staff".prevent_profile_update_operation_update();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_profile_update_operations_staff_members_ScopeId_StaffMember~",
                schema: "staff",
                table: "profile_update_operations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_profile_update_operations",
                schema: "staff",
                table: "profile_update_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_profile_update_operations_fingerprint",
                schema: "staff",
                table: "profile_update_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_profile_update_operations_status",
                schema: "staff",
                table: "profile_update_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_profile_update_operations_versions",
                schema: "staff",
                table: "profile_update_operations");

            migrationBuilder.RenameTable(
                name: "profile_update_operations",
                schema: "staff",
                newName: "member_mutation_operations",
                newSchema: "staff");

            migrationBuilder.RenameIndex(
                name: "IX_profile_update_operations_ScopeId_CompletedAtUtc_Id",
                schema: "staff",
                table: "member_mutation_operations",
                newName: "IX_member_mutation_operations_ScopeId_CompletedAtUtc_Id");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "staff",
                table: "member_mutation_operations",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "staff"."member_mutation_operations"
                SET "Kind" = 1
                WHERE "Kind" IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Kind",
                schema: "staff",
                table: "member_mutation_operations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_member_mutation_operations",
                schema: "staff",
                table: "member_mutation_operations",
                columns: new[] { "ScopeId", "StaffMemberId", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_fingerprint",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "char_length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"Kind\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_status",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"ResultStatus\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_member_mutation_operations_versions",
                schema: "staff",
                table: "member_mutation_operations",
                sql: "\"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + 1");

            migrationBuilder.AddForeignKey(
                name: "FK_member_mutation_operations_staff_members_ScopeId_StaffMembe~",
                schema: "staff",
                table: "member_mutation_operations",
                columns: new[] { "ScopeId", "StaffMemberId" },
                principalSchema: "staff",
                principalTable: "staff_members",
                principalColumns: new[] { "ScopeId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION "staff".prevent_member_mutation_operation_update()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'Staff member-mutation operations are immutable';
                END;
                $function$;

                CREATE TRIGGER "TR_staff_member_mutation_operations_immutable"
                BEFORE UPDATE ON "staff"."member_mutation_operations"
                FOR EACH ROW
                EXECUTE FUNCTION "staff".prevent_member_mutation_operation_update();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS
                    "TR_staff_member_mutation_operations_immutable"
                    ON "staff"."member_mutation_operations";
                DROP FUNCTION IF EXISTS
                    "staff".prevent_member_mutation_operation_update();

                DELETE FROM "staff"."member_mutation_operations"
                WHERE "Kind" <> 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_member_mutation_operations_staff_members_ScopeId_StaffMembe~",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_member_mutation_operations",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_fingerprint",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_kind",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_status",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_staff_member_mutation_operations_versions",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "staff",
                table: "member_mutation_operations");

            migrationBuilder.RenameTable(
                name: "member_mutation_operations",
                schema: "staff",
                newName: "profile_update_operations",
                newSchema: "staff");

            migrationBuilder.RenameIndex(
                name: "IX_member_mutation_operations_ScopeId_CompletedAtUtc_Id",
                schema: "staff",
                table: "profile_update_operations",
                newName: "IX_profile_update_operations_ScopeId_CompletedAtUtc_Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_profile_update_operations",
                schema: "staff",
                table: "profile_update_operations",
                columns: new[] { "ScopeId", "StaffMemberId", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_profile_update_operations_fingerprint",
                schema: "staff",
                table: "profile_update_operations",
                sql: "char_length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_profile_update_operations_status",
                schema: "staff",
                table: "profile_update_operations",
                sql: "\"ResultStatus\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_staff_profile_update_operations_versions",
                schema: "staff",
                table: "profile_update_operations",
                sql: "\"ExpectedVersion\" > 0 AND \"ResultVersion\" >= \"ExpectedVersion\" AND \"ResultVersion\" <= \"ExpectedVersion\" + 1");

            migrationBuilder.AddForeignKey(
                name: "FK_profile_update_operations_staff_members_ScopeId_StaffMember~",
                schema: "staff",
                table: "profile_update_operations",
                columns: new[] { "ScopeId", "StaffMemberId" },
                principalSchema: "staff",
                principalTable: "staff_members",
                principalColumns: new[] { "ScopeId", "Id" },
                onDelete: ReferentialAction.Cascade);

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
    }
}
