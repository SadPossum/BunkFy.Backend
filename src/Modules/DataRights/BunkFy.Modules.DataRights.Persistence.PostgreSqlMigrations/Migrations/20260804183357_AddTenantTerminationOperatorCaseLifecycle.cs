using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTerminationOperatorCaseLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_approval_policy_evidence",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddColumn<bool>(
                name: "TenantTerminationExportRequested",
                schema: "data-rights",
                table: "cases",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantTerminationPolicyEvidenceSha256",
                schema: "data-rights",
                table: "cases",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "data-rights"."cases" AS cases
                SET
                    "TenantTerminationExportRequested" = process."ExportRequested",
                    "TenantTerminationPolicyEvidenceSha256" = process."PolicyEvidenceSha256",
                    "ApprovalEvidenceSchemaVersion" = NULL,
                    "ApprovalEvidenceCaseKind" = NULL,
                    "ApprovalEvidenceScopeKind" = NULL,
                    "ApprovalEvidencePropertyId" = NULL,
                    "ApprovalEvidencePropertyVersion" = NULL,
                    "ApprovalEvidenceOperatingCountryCode" = NULL,
                    "ApprovalEvidencePolicyId" = NULL,
                    "ApprovalEvidencePolicyVersion" = NULL,
                    "ApprovalEvidenceRetentionPolicyId" = NULL,
                    "ApprovalEvidenceRetentionPolicyVersion" = NULL,
                    "ApprovalEvidenceContentSha256" = NULL,
                    "ApprovalEvidencePurposeCode" = NULL,
                    "ApprovalEvidenceSurface" = NULL,
                    "ApprovalEvidenceSourceProvenance" = NULL,
                    "ApprovalEvidenceRetentionDataClass" = NULL,
                    "ApprovalEvidenceRetentionTrigger" = NULL,
                    "ApprovalEvidenceRetentionTriggeredAtUtc" = NULL,
                    "ApprovalEvidenceRetentionDeadlineUtc" = NULL,
                    "ApprovalEvidenceEvaluatedAtUtc" = NULL,
                    "ApprovalEvidenceStateBindingsJson" = NULL,
                    "ApprovalEvidenceStateBindingsSha256" = NULL,
                    "ApprovalEvidenceRequiresDistinctExecutor" = NULL
                FROM "data-rights"."tenant_termination_processes" AS process
                WHERE cases."Kind" = 2
                    AND cases."ScopeId" = process."ScopeId"
                    AND cases."Id" = process."CaseId";

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."cases"
                        WHERE "Kind" = 2
                            AND (
                                "TenantTerminationExportRequested" IS NULL OR
                                "TenantTerminationPolicyEvidenceSha256" IS NULL
                            )
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot upgrade Data Rights while a tenant-termination case lacks an exact coordinator process.';
                    END IF;

                    IF EXISTS (
                        SELECT "ScopeId"
                        FROM "data-rights"."cases"
                        WHERE "Kind" = 2 AND "Status" NOT IN (6, 9, 11)
                        GROUP BY "ScopeId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot upgrade Data Rights while a tenant has multiple active termination cases.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_data_rights_cases_active_tenant_termination",
                schema: "data-rights",
                table: "cases",
                column: "ScopeId",
                unique: true,
                filter: "\"Kind\" = 2 AND \"Status\" NOT IN (6, 9, 11)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_approval_policy_evidence",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" <> 2 AND \"Decision\" = 1 AND \"RequestedOperations\" = 16 AND \"ApprovalEvidenceSchemaVersion\" IN (1, 2) AND \"ApprovalEvidenceCaseKind\" = \"Kind\" AND \"ApprovalEvidenceOperatingCountryCode\" IS NOT NULL AND char_length(\"ApprovalEvidenceOperatingCountryCode\") = 2 AND \"ApprovalEvidencePolicyId\" IS NOT NULL AND length(trim(\"ApprovalEvidencePolicyId\")) > 0 AND \"ApprovalEvidencePolicyVersion\" > 0 AND \"ApprovalEvidenceRetentionPolicyId\" IS NOT NULL AND length(trim(\"ApprovalEvidenceRetentionPolicyId\")) > 0 AND \"ApprovalEvidenceRetentionPolicyVersion\" > 0 AND \"ApprovalEvidenceContentSha256\" IS NOT NULL AND char_length(\"ApprovalEvidenceContentSha256\") = 64 AND \"ApprovalEvidencePurposeCode\" IS NOT NULL AND length(trim(\"ApprovalEvidencePurposeCode\")) > 0 AND \"ApprovalEvidenceSurface\" = 'erasure' AND \"ApprovalEvidenceSourceProvenance\" IS NOT NULL AND length(trim(\"ApprovalEvidenceSourceProvenance\")) > 0 AND \"ApprovalEvidenceEvaluatedAtUtc\" IS NOT NULL AND \"ApprovalEvidenceRequiresDistinctExecutor\" = TRUE AND ((\"ApprovalEvidenceSchemaVersion\" = 1 AND \"Kind\" = 1 AND \"ApprovalEvidenceScopeKind\" = 1 AND \"ApprovalEvidencePropertyId\" = \"PropertyId\" AND \"ApprovalEvidencePropertyVersion\" > 0 AND \"ApprovalEvidencePurposeCode\" = 'data-rights-anonymisation' AND \"ApprovalEvidenceSourceProvenance\" = 'authorized-workspace-operator' AND \"ApprovalEvidenceRetentionDataClass\" = '' AND \"ApprovalEvidenceRetentionTrigger\" = '' AND \"ApprovalEvidenceRetentionTriggeredAtUtc\" IS NULL AND \"ApprovalEvidenceRetentionDeadlineUtc\" IS NULL AND \"ApprovalEvidenceStateBindingsJson\" = '[]' AND \"ApprovalEvidenceStateBindingsSha256\" IS NOT NULL AND char_length(\"ApprovalEvidenceStateBindingsSha256\") = 64) OR (\"ApprovalEvidenceSchemaVersion\" = 2 AND ((\"Kind\" = 1 AND \"ApprovalEvidenceScopeKind\" = 1 AND \"ApprovalEvidencePropertyId\" = \"PropertyId\" AND \"ApprovalEvidencePropertyVersion\" > 0) OR (\"Kind\" IN (2, 3) AND \"ApprovalEvidenceScopeKind\" = 2 AND \"PropertyId\" IS NULL AND \"ApprovalEvidencePropertyId\" IS NULL AND \"ApprovalEvidencePropertyVersion\" = 0)) AND \"ApprovalEvidenceRetentionDataClass\" IS NOT NULL AND length(trim(\"ApprovalEvidenceRetentionDataClass\")) > 0 AND \"ApprovalEvidenceRetentionTrigger\" IS NOT NULL AND length(trim(\"ApprovalEvidenceRetentionTrigger\")) > 0 AND \"ApprovalEvidenceRetentionTriggeredAtUtc\" IS NOT NULL AND \"ApprovalEvidenceRetentionDeadlineUtc\" > \"ApprovalEvidenceRetentionTriggeredAtUtc\" AND \"ApprovalEvidenceRetentionDeadlineUtc\" <= \"ApprovalEvidenceEvaluatedAtUtc\" AND \"ApprovalEvidenceStateBindingsJson\" IS NOT NULL AND char_length(\"ApprovalEvidenceStateBindingsJson\") > 2 AND \"ApprovalEvidenceStateBindingsSha256\" IS NOT NULL AND char_length(\"ApprovalEvidenceStateBindingsSha256\") = 64))) OR ((\"Kind\" = 2 OR \"Decision\" <> 1 OR \"RequestedOperations\" <> 16) AND \"ApprovalEvidenceSchemaVersion\" IS NULL AND \"ApprovalEvidenceCaseKind\" IS NULL AND \"ApprovalEvidenceScopeKind\" IS NULL AND \"ApprovalEvidencePropertyId\" IS NULL AND \"ApprovalEvidencePropertyVersion\" IS NULL AND \"ApprovalEvidenceOperatingCountryCode\" IS NULL AND \"ApprovalEvidencePolicyId\" IS NULL AND \"ApprovalEvidencePolicyVersion\" IS NULL AND \"ApprovalEvidenceRetentionPolicyId\" IS NULL AND \"ApprovalEvidenceRetentionPolicyVersion\" IS NULL AND \"ApprovalEvidenceContentSha256\" IS NULL AND \"ApprovalEvidencePurposeCode\" IS NULL AND \"ApprovalEvidenceSurface\" IS NULL AND \"ApprovalEvidenceSourceProvenance\" IS NULL AND \"ApprovalEvidenceRetentionDataClass\" IS NULL AND \"ApprovalEvidenceRetentionTrigger\" IS NULL AND \"ApprovalEvidenceRetentionTriggeredAtUtc\" IS NULL AND \"ApprovalEvidenceRetentionDeadlineUtc\" IS NULL AND \"ApprovalEvidenceEvaluatedAtUtc\" IS NULL AND \"ApprovalEvidenceStateBindingsJson\" IS NULL AND \"ApprovalEvidenceStateBindingsSha256\" IS NULL AND \"ApprovalEvidenceRequiresDistinctExecutor\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" = 1 AND \"RequestedOperations\" BETWEEN 1 AND 31) OR (\"Kind\" = 2 AND \"RequestedOperations\" = 16) OR (\"Kind\" = 3 AND \"RequestedOperations\" IN (1, 2, 4, 16))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_tenant_termination",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" = 2 AND \"TenantTerminationExportRequested\" IS NOT NULL AND ((\"Decision\" = 1 AND \"TenantTerminationPolicyEvidenceSha256\" IS NOT NULL AND char_length(\"TenantTerminationPolicyEvidenceSha256\") = 64 AND \"TenantTerminationPolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$') OR (\"Decision\" <> 1 AND \"TenantTerminationPolicyEvidenceSha256\" IS NULL))) OR (\"Kind\" <> 2 AND \"TenantTerminationExportRequested\" IS NULL AND \"TenantTerminationPolicyEvidenceSha256\" IS NULL)");
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
                        FROM "data-rights"."cases"
                        WHERE "Kind" = 2
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade Data Rights while tenant-termination operator cases exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "UX_data_rights_cases_active_tenant_termination",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_approval_policy_evidence",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_tenant_termination",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "TenantTerminationExportRequested",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "TenantTerminationPolicyEvidenceSha256",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_approval_policy_evidence",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Decision\" = 1 AND \"RequestedOperations\" = 16 AND \"ApprovalEvidenceSchemaVersion\" IN (1, 2) AND \"ApprovalEvidenceCaseKind\" = \"Kind\" AND \"ApprovalEvidenceOperatingCountryCode\" IS NOT NULL AND char_length(\"ApprovalEvidenceOperatingCountryCode\") = 2 AND \"ApprovalEvidencePolicyId\" IS NOT NULL AND length(trim(\"ApprovalEvidencePolicyId\")) > 0 AND \"ApprovalEvidencePolicyVersion\" > 0 AND \"ApprovalEvidenceRetentionPolicyId\" IS NOT NULL AND length(trim(\"ApprovalEvidenceRetentionPolicyId\")) > 0 AND \"ApprovalEvidenceRetentionPolicyVersion\" > 0 AND \"ApprovalEvidenceContentSha256\" IS NOT NULL AND char_length(\"ApprovalEvidenceContentSha256\") = 64 AND \"ApprovalEvidencePurposeCode\" IS NOT NULL AND length(trim(\"ApprovalEvidencePurposeCode\")) > 0 AND \"ApprovalEvidenceSurface\" = 'erasure' AND \"ApprovalEvidenceSourceProvenance\" IS NOT NULL AND length(trim(\"ApprovalEvidenceSourceProvenance\")) > 0 AND \"ApprovalEvidenceEvaluatedAtUtc\" IS NOT NULL AND \"ApprovalEvidenceRequiresDistinctExecutor\" = TRUE AND ((\"ApprovalEvidenceSchemaVersion\" = 1 AND \"Kind\" = 1 AND \"ApprovalEvidenceScopeKind\" = 1 AND \"ApprovalEvidencePropertyId\" = \"PropertyId\" AND \"ApprovalEvidencePropertyVersion\" > 0 AND \"ApprovalEvidencePurposeCode\" = 'data-rights-anonymisation' AND \"ApprovalEvidenceSourceProvenance\" = 'authorized-workspace-operator' AND \"ApprovalEvidenceRetentionDataClass\" = '' AND \"ApprovalEvidenceRetentionTrigger\" = '' AND \"ApprovalEvidenceRetentionTriggeredAtUtc\" IS NULL AND \"ApprovalEvidenceRetentionDeadlineUtc\" IS NULL AND \"ApprovalEvidenceStateBindingsJson\" = '[]' AND \"ApprovalEvidenceStateBindingsSha256\" IS NOT NULL AND char_length(\"ApprovalEvidenceStateBindingsSha256\") = 64) OR (\"ApprovalEvidenceSchemaVersion\" = 2 AND ((\"Kind\" = 1 AND \"ApprovalEvidenceScopeKind\" = 1 AND \"ApprovalEvidencePropertyId\" = \"PropertyId\" AND \"ApprovalEvidencePropertyVersion\" > 0) OR (\"Kind\" IN (2, 3) AND \"ApprovalEvidenceScopeKind\" = 2 AND \"PropertyId\" IS NULL AND \"ApprovalEvidencePropertyId\" IS NULL AND \"ApprovalEvidencePropertyVersion\" = 0)) AND \"ApprovalEvidenceRetentionDataClass\" IS NOT NULL AND length(trim(\"ApprovalEvidenceRetentionDataClass\")) > 0 AND \"ApprovalEvidenceRetentionTrigger\" IS NOT NULL AND length(trim(\"ApprovalEvidenceRetentionTrigger\")) > 0 AND \"ApprovalEvidenceRetentionTriggeredAtUtc\" IS NOT NULL AND \"ApprovalEvidenceRetentionDeadlineUtc\" > \"ApprovalEvidenceRetentionTriggeredAtUtc\" AND \"ApprovalEvidenceRetentionDeadlineUtc\" <= \"ApprovalEvidenceEvaluatedAtUtc\" AND \"ApprovalEvidenceStateBindingsJson\" IS NOT NULL AND char_length(\"ApprovalEvidenceStateBindingsJson\") > 2 AND \"ApprovalEvidenceStateBindingsSha256\" IS NOT NULL AND char_length(\"ApprovalEvidenceStateBindingsSha256\") = 64))) OR ((\"Decision\" <> 1 OR \"RequestedOperations\" <> 16) AND \"ApprovalEvidenceSchemaVersion\" IS NULL AND \"ApprovalEvidenceCaseKind\" IS NULL AND \"ApprovalEvidenceScopeKind\" IS NULL AND \"ApprovalEvidencePropertyId\" IS NULL AND \"ApprovalEvidencePropertyVersion\" IS NULL AND \"ApprovalEvidenceOperatingCountryCode\" IS NULL AND \"ApprovalEvidencePolicyId\" IS NULL AND \"ApprovalEvidencePolicyVersion\" IS NULL AND \"ApprovalEvidenceRetentionPolicyId\" IS NULL AND \"ApprovalEvidenceRetentionPolicyVersion\" IS NULL AND \"ApprovalEvidenceContentSha256\" IS NULL AND \"ApprovalEvidencePurposeCode\" IS NULL AND \"ApprovalEvidenceSurface\" IS NULL AND \"ApprovalEvidenceSourceProvenance\" IS NULL AND \"ApprovalEvidenceRetentionDataClass\" IS NULL AND \"ApprovalEvidenceRetentionTrigger\" IS NULL AND \"ApprovalEvidenceRetentionTriggeredAtUtc\" IS NULL AND \"ApprovalEvidenceRetentionDeadlineUtc\" IS NULL AND \"ApprovalEvidenceEvaluatedAtUtc\" IS NULL AND \"ApprovalEvidenceStateBindingsJson\" IS NULL AND \"ApprovalEvidenceStateBindingsSha256\" IS NULL AND \"ApprovalEvidenceRequiresDistinctExecutor\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" <> 3 AND \"RequestedOperations\" BETWEEN 1 AND 31) OR (\"Kind\" = 3 AND \"RequestedOperations\" IN (1, 2, 4, 16))");
        }
    }
}
