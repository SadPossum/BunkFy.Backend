using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class SupportTenantScopedCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_correction_executions_ScopeId_PropertyId_State_ExpiresAtUtc",
                schema: "data-rights",
                table: "correction_executions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_correction_executions_contract",
                schema: "data-rights",
                table: "correction_executions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_correction_executions_coordinates",
                schema: "data-rights",
                table: "correction_executions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                schema: "data-rights",
                table: "correction_executions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "CaseKind",
                schema: "data-rights",
                table: "correction_executions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE "data-rights"."correction_executions"
                SET "ContractVersion" = 2
                WHERE "ContractVersion" = 1;

                ALTER TABLE "data-rights"."correction_executions"
                ALTER COLUMN "CaseKind" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_correction_executions_ScopeId_CaseKind_PropertyId_State_Exp~",
                schema: "data-rights",
                table: "correction_executions",
                columns: new[] { "ScopeId", "CaseKind", "PropertyId", "State", "ExpiresAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_correction_executions_contract",
                schema: "data-rights",
                table: "correction_executions",
                sql: "\"ContractVersion\" = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_correction_executions_coordinates",
                schema: "data-rights",
                table: "correction_executions",
                sql: "((\"CaseKind\" = 1 AND \"PropertyId\" IS NOT NULL) OR (\"CaseKind\" IN (2, 3) AND \"PropertyId\" IS NULL)) AND \"CaseId\" IS NOT NULL AND \"RecordId\" IS NOT NULL AND length(trim(\"OwnerKey\")) > 0 AND length(trim(\"RecordType\")) > 0 AND length(trim(\"FieldPolicyKey\")) > 0 AND length(trim(\"ExecutedBy\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" <> 3 AND \"RequestedOperations\" BETWEEN 1 AND 31) OR (\"Kind\" = 3 AND \"RequestedOperations\" IN (1, 2))");
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
                        FROM "data-rights"."correction_executions"
                        WHERE "CaseKind" <> 1 OR "PropertyId" IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while tenant-scoped correction executions exist.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."cases"
                        WHERE "Kind" = 3 AND "RequestedOperations" <> 1
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while Staff correction cases exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_correction_executions_ScopeId_CaseKind_PropertyId_State_Exp~",
                schema: "data-rights",
                table: "correction_executions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_correction_executions_contract",
                schema: "data-rights",
                table: "correction_executions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_correction_executions_coordinates",
                schema: "data-rights",
                table: "correction_executions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.Sql(
                """
                UPDATE "data-rights"."correction_executions"
                SET "ContractVersion" = 1
                WHERE "ContractVersion" = 2;
                """);

            migrationBuilder.DropColumn(
                name: "CaseKind",
                schema: "data-rights",
                table: "correction_executions");

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                schema: "data-rights",
                table: "correction_executions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_correction_executions_ScopeId_PropertyId_State_ExpiresAtUtc",
                schema: "data-rights",
                table: "correction_executions",
                columns: new[] { "ScopeId", "PropertyId", "State", "ExpiresAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_correction_executions_contract",
                schema: "data-rights",
                table: "correction_executions",
                sql: "\"ContractVersion\" = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_correction_executions_coordinates",
                schema: "data-rights",
                table: "correction_executions",
                sql: "\"PropertyId\" IS NOT NULL AND \"CaseId\" IS NOT NULL AND \"RecordId\" IS NOT NULL AND length(trim(\"OwnerKey\")) > 0 AND length(trim(\"RecordType\")) > 0 AND length(trim(\"FieldPolicyKey\")) > 0 AND length(trim(\"ExecutedBy\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" <> 3 AND \"RequestedOperations\" BETWEEN 1 AND 31) OR (\"Kind\" = 3 AND \"RequestedOperations\" = 1)");
        }
    }
}
