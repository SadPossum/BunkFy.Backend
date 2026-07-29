using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ScopeAnonymisationExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_contract",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_policy",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_result_version",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_execution_work_items_ScopeId_PropertyId_State_CreatedAtUtc_~",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_policy",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_approval_policy_evidence",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoutingPropertyId",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "CaseKind",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyEvaluatedAtUtc",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyOperatingCountryCode",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PolicyPropertyVersion",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyPurposeCode",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PolicyRequiresDistinctExecutor",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyRetentionDataClass",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyRetentionDeadlineUtc",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyRetentionTrigger",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyRetentionTriggeredAtUtc",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySourceProvenance",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyStateBindingsJson",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyStateBindingsSha256",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySurface",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeKind",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                schema: "data-rights",
                table: "execution_work_items",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "CaseKind",
                schema: "data-rights",
                table: "execution_work_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyEvaluatedAtUtc",
                schema: "data-rights",
                table: "execution_work_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyOperatingCountryCode",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PolicyPropertyVersion",
                schema: "data-rights",
                table: "execution_work_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyPurposeCode",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PolicyRequiresDistinctExecutor",
                schema: "data-rights",
                table: "execution_work_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyRetentionDataClass",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyRetentionDeadlineUtc",
                schema: "data-rights",
                table: "execution_work_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyRetentionTrigger",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyRetentionTriggeredAtUtc",
                schema: "data-rights",
                table: "execution_work_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySourceProvenance",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyStateBindingsJson",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyStateBindingsSha256",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySurface",
                schema: "data-rights",
                table: "execution_work_items",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeKind",
                schema: "data-rights",
                table: "execution_work_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                schema: "data-rights",
                table: "execution_batches",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "CaseKind",
                schema: "data-rights",
                table: "execution_batches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeKind",
                schema: "data-rights",
                table: "execution_batches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalEvidenceCaseKind",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceRetentionDataClass",
                schema: "data-rights",
                table: "cases",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovalEvidenceRetentionDeadlineUtc",
                schema: "data-rights",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceRetentionTrigger",
                schema: "data-rights",
                table: "cases",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovalEvidenceRetentionTriggeredAtUtc",
                schema: "data-rights",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalEvidenceScopeKind",
                schema: "data-rights",
                table: "cases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceStateBindingsJson",
                schema: "data-rights",
                table: "cases",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceStateBindingsSha256",
                schema: "data-rights",
                table: "cases",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."processing_ledger_entries"
                        WHERE "ContractVersion" NOT IN (1, 2)
                           OR "RoutingPropertyId" IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot upgrade unexpected processing-ledger contracts to scoped execution.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."cases"
                        WHERE "Decision" = 1
                          AND "RequestedOperations" = 16
                          AND (
                              "Kind" <> 1
                              OR "PropertyId" IS NULL
                              OR "ApprovalEvidenceSchemaVersion" <> 1
                              OR "ApprovalEvidencePropertyId" IS DISTINCT FROM "PropertyId"
                              OR "ApprovalEvidencePropertyVersion" IS NULL
                              OR "ApprovalEvidencePropertyVersion" < 1
                              OR "ApprovalEvidenceOperatingCountryCode" IS NULL
                              OR "ApprovalEvidencePurposeCode" IS NULL
                              OR "ApprovalEvidenceSurface" IS NULL
                              OR "ApprovalEvidenceSourceProvenance" IS NULL
                              OR "ApprovalEvidenceEvaluatedAtUtc" IS NULL
                              OR "ApprovalEvidenceRequiresDistinctExecutor" IS DISTINCT FROM TRUE
                          )
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot upgrade non-Guest or incomplete legacy anonymisation approval evidence.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."execution_batches" AS b
                        LEFT JOIN "data-rights"."cases" AS c
                          ON c."ScopeId" = b."ScopeId"
                         AND c."Id" = b."CaseId"
                        WHERE c."Id" IS NULL
                           OR c."Kind" <> 1
                           OR c."Decision" <> 1
                           OR c."RequestedOperations" <> 16
                           OR c."ApprovalEvidenceSchemaVersion" <> 1
                           OR b."PropertyId" IS DISTINCT FROM c."PropertyId"
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot upgrade an execution batch that is not backed by a legacy Guest anonymisation approval.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."execution_work_items" AS w
                        LEFT JOIN "data-rights"."cases" AS c
                          ON c."ScopeId" = w."ScopeId"
                         AND c."Id" = w."CaseId"
                        WHERE c."Id" IS NULL
                           OR c."Kind" <> 1
                           OR c."Decision" <> 1
                           OR c."RequestedOperations" <> 16
                           OR c."ApprovalEvidenceSchemaVersion" <> 1
                           OR w."PropertyId" IS DISTINCT FROM c."PropertyId"
                           OR w."PolicyEvidenceSchemaVersion" <> c."ApprovalEvidenceSchemaVersion"
                           OR w."PolicyId" IS DISTINCT FROM c."ApprovalEvidencePolicyId"
                           OR w."PolicyVersion" IS DISTINCT FROM c."ApprovalEvidencePolicyVersion"
                           OR w."RetentionPolicyId" IS DISTINCT FROM c."ApprovalEvidenceRetentionPolicyId"
                           OR w."RetentionPolicyVersion" IS DISTINCT FROM c."ApprovalEvidenceRetentionPolicyVersion"
                           OR w."PolicyContentSha256" IS DISTINCT FROM c."ApprovalEvidenceContentSha256"
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot upgrade an execution work item with mismatched legacy approval evidence.';
                    END IF;
                END
                $$;

                UPDATE "data-rights"."cases"
                SET "ApprovalEvidenceCaseKind" = "Kind",
                    "ApprovalEvidenceScopeKind" = 1,
                    "ApprovalEvidenceRetentionDataClass" = '',
                    "ApprovalEvidenceRetentionTrigger" = '',
                    "ApprovalEvidenceRetentionTriggeredAtUtc" = NULL,
                    "ApprovalEvidenceRetentionDeadlineUtc" = NULL,
                    "ApprovalEvidenceStateBindingsJson" = '[]',
                    "ApprovalEvidenceStateBindingsSha256" =
                        '4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945'
                WHERE "Decision" = 1
                  AND "RequestedOperations" = 16;

                UPDATE "data-rights"."execution_batches"
                SET "CaseKind" = 1,
                    "ScopeKind" = 1;

                UPDATE "data-rights"."execution_work_items" AS w
                SET "CaseKind" = 1,
                    "ScopeKind" = 1,
                    "PolicyPropertyVersion" = c."ApprovalEvidencePropertyVersion",
                    "PolicyOperatingCountryCode" = c."ApprovalEvidenceOperatingCountryCode",
                    "PolicyPurposeCode" = c."ApprovalEvidencePurposeCode",
                    "PolicySurface" = c."ApprovalEvidenceSurface",
                    "PolicySourceProvenance" = c."ApprovalEvidenceSourceProvenance",
                    "PolicyRetentionDataClass" = '',
                    "PolicyRetentionTrigger" = '',
                    "PolicyRetentionTriggeredAtUtc" = NULL,
                    "PolicyRetentionDeadlineUtc" = NULL,
                    "PolicyEvaluatedAtUtc" = c."ApprovalEvidenceEvaluatedAtUtc",
                    "PolicyStateBindingsJson" = '[]',
                    "PolicyStateBindingsSha256" =
                        '4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945',
                    "PolicyRequiresDistinctExecutor" =
                        c."ApprovalEvidenceRequiresDistinctExecutor"
                FROM "data-rights"."cases" AS c
                WHERE c."ScopeId" = w."ScopeId"
                  AND c."Id" = w."CaseId";

                ALTER TABLE "data-rights"."processing_ledger_entries"
                    DISABLE TRIGGER "TR_processing_ledger_entries_append_only";

                UPDATE "data-rights"."processing_ledger_entries"
                SET "CaseKind" = 0,
                    "ScopeKind" = 0;

                ALTER TABLE "data-rights"."processing_ledger_entries"
                    ENABLE TRIGGER "TR_processing_ledger_entries_append_only";

                ALTER TABLE "data-rights"."processing_ledger_entries"
                    ALTER COLUMN "CaseKind" SET NOT NULL,
                    ALTER COLUMN "ScopeKind" SET NOT NULL;

                ALTER TABLE "data-rights"."execution_batches"
                    ALTER COLUMN "CaseKind" SET NOT NULL,
                    ALTER COLUMN "ScopeKind" SET NOT NULL;

                ALTER TABLE "data-rights"."execution_work_items"
                    ALTER COLUMN "CaseKind" SET NOT NULL,
                    ALTER COLUMN "ScopeKind" SET NOT NULL,
                    ALTER COLUMN "PolicyPropertyVersion" SET NOT NULL,
                    ALTER COLUMN "PolicyOperatingCountryCode" SET NOT NULL,
                    ALTER COLUMN "PolicyPurposeCode" SET NOT NULL,
                    ALTER COLUMN "PolicySurface" SET NOT NULL,
                    ALTER COLUMN "PolicySourceProvenance" SET NOT NULL,
                    ALTER COLUMN "PolicyRetentionDataClass" SET NOT NULL,
                    ALTER COLUMN "PolicyRetentionTrigger" SET NOT NULL,
                    ALTER COLUMN "PolicyEvaluatedAtUtc" SET NOT NULL,
                    ALTER COLUMN "PolicyStateBindingsJson" SET NOT NULL,
                    ALTER COLUMN "PolicyStateBindingsSha256" SET NOT NULL,
                    ALTER COLUMN "PolicyRequiresDistinctExecutor" SET NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_contract",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "\"ContractVersion\" BETWEEN 1 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_policy",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "\"PolicyEvidenceSchemaVersion\" IN (1, 2) AND length(trim(\"PolicyId\")) > 0 AND \"PolicyVersion\" >= 1 AND length(trim(\"RetentionPolicyId\")) > 0 AND \"RetentionPolicyVersion\" >= 1 AND char_length(\"PolicyContentSha256\") = 64 AND ((\"ContractVersion\" IN (1, 2) AND \"PolicyEvidenceSchemaVersion\" = 1 AND \"PolicyPropertyVersion\" IS NULL AND \"PolicyOperatingCountryCode\" IS NULL AND \"PolicyPurposeCode\" IS NULL AND \"PolicySurface\" IS NULL AND \"PolicySourceProvenance\" IS NULL AND \"PolicyRetentionDataClass\" IS NULL AND \"PolicyRetentionTrigger\" IS NULL AND \"PolicyRetentionTriggeredAtUtc\" IS NULL AND \"PolicyRetentionDeadlineUtc\" IS NULL AND \"PolicyEvaluatedAtUtc\" IS NULL AND \"PolicyStateBindingsJson\" IS NULL AND \"PolicyStateBindingsSha256\" IS NULL AND \"PolicyRequiresDistinctExecutor\" IS NULL) OR (\"ContractVersion\" = 3 AND \"PolicyEvidenceSchemaVersion\" = 2 AND ((\"ScopeKind\" = 1 AND \"PolicyPropertyVersion\" >= 1) OR (\"ScopeKind\" = 2 AND \"PolicyPropertyVersion\" = 0)) AND char_length(\"PolicyOperatingCountryCode\") = 2 AND length(trim(\"PolicyPurposeCode\")) > 0 AND length(trim(\"PolicySurface\")) > 0 AND length(trim(\"PolicySourceProvenance\")) > 0 AND length(trim(\"PolicyRetentionDataClass\")) > 0 AND length(trim(\"PolicyRetentionTrigger\")) > 0 AND \"PolicyRetentionTriggeredAtUtc\" IS NOT NULL AND \"PolicyRetentionDeadlineUtc\" > \"PolicyRetentionTriggeredAtUtc\" AND \"PolicyRetentionDeadlineUtc\" <= \"PolicyEvaluatedAtUtc\" AND length(\"PolicyStateBindingsJson\") > 0 AND length(\"PolicyStateBindingsJson\") <= 4096 AND char_length(\"PolicyStateBindingsSha256\") = 64 AND \"PolicyRequiresDistinctExecutor\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_result_version",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "(\"ContractVersion\" = 1 AND \"ResultingRecordVersion\" IS NULL) OR (\"ContractVersion\" >= 2 AND \"ResultingRecordVersion\" >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_scope",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "((\"ContractVersion\" IN (1, 2) AND \"CaseKind\" = 0 AND \"ScopeKind\" = 0 AND \"RoutingPropertyId\" IS NOT NULL) OR (\"ContractVersion\" = 3 AND ((\"CaseKind\" = 1 AND \"ScopeKind\" = 1 AND \"RoutingPropertyId\" IS NOT NULL) OR (\"CaseKind\" IN (2, 3) AND \"ScopeKind\" = 2 AND \"RoutingPropertyId\" IS NULL))))");

            migrationBuilder.CreateIndex(
                name: "IX_execution_work_items_ScopeId_CaseKind_ScopeKind_PropertyId_~",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "CaseKind", "ScopeKind", "PropertyId", "State", "CreatedAtUtc", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_policy",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "\"PolicyEvidenceSchemaVersion\" IN (1, 2) AND \"PolicyPropertyVersion\" >= 0 AND char_length(\"PolicyOperatingCountryCode\") = 2 AND length(trim(\"PolicyId\")) > 0 AND \"PolicyVersion\" >= 1 AND length(trim(\"RetentionPolicyId\")) > 0 AND \"RetentionPolicyVersion\" >= 1 AND char_length(\"PolicyContentSha256\") = 64 AND length(trim(\"PolicyPurposeCode\")) > 0 AND \"PolicySurface\" = 'erasure' AND length(trim(\"PolicySourceProvenance\")) > 0 AND \"PolicyEvaluatedAtUtc\" IS NOT NULL AND length(\"PolicyStateBindingsJson\") > 0 AND length(\"PolicyStateBindingsJson\") <= 4096 AND char_length(\"PolicyStateBindingsSha256\") = 64 AND \"PolicyRequiresDistinctExecutor\" AND ((\"PolicyEvidenceSchemaVersion\" = 1 AND \"CaseKind\" = 1 AND \"ScopeKind\" = 1 AND \"PolicyPropertyVersion\" >= 1 AND \"PolicyRetentionDataClass\" = '' AND \"PolicyRetentionTrigger\" = '' AND \"PolicyRetentionTriggeredAtUtc\" IS NULL AND \"PolicyRetentionDeadlineUtc\" IS NULL) OR (\"PolicyEvidenceSchemaVersion\" = 2 AND ((\"ScopeKind\" = 1 AND \"PolicyPropertyVersion\" >= 1) OR (\"ScopeKind\" = 2 AND \"PolicyPropertyVersion\" = 0)) AND length(trim(\"PolicyRetentionDataClass\")) > 0 AND length(trim(\"PolicyRetentionTrigger\")) > 0 AND \"PolicyRetentionTriggeredAtUtc\" IS NOT NULL AND \"PolicyRetentionDeadlineUtc\" > \"PolicyRetentionTriggeredAtUtc\" AND \"PolicyRetentionDeadlineUtc\" <= \"PolicyEvaluatedAtUtc\"))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_scope",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "(\"CaseKind\" = 1 AND \"ScopeKind\" = 1 AND \"PropertyId\" IS NOT NULL) OR (\"CaseKind\" IN (2, 3) AND \"ScopeKind\" = 2 AND \"PropertyId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_batches_scope",
                schema: "data-rights",
                table: "execution_batches",
                sql: "(\"CaseKind\" = 1 AND \"ScopeKind\" = 1 AND \"PropertyId\" IS NOT NULL) OR (\"CaseKind\" IN (2, 3) AND \"ScopeKind\" = 2 AND \"PropertyId\" IS NULL)");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."processing_ledger_entries"
                        WHERE "ContractVersion" NOT IN (1, 2)
                           OR "CaseKind" <> 0
                           OR "ScopeKind" <> 0
                           OR "RoutingPropertyId" IS NULL
                           OR "PolicyPropertyVersion" IS NOT NULL
                           OR "PolicyOperatingCountryCode" IS NOT NULL
                           OR "PolicyPurposeCode" IS NOT NULL
                           OR "PolicySurface" IS NOT NULL
                           OR "PolicySourceProvenance" IS NOT NULL
                           OR "PolicyRetentionDataClass" IS NOT NULL
                           OR "PolicyRetentionTrigger" IS NOT NULL
                           OR "PolicyRetentionTriggeredAtUtc" IS NOT NULL
                           OR "PolicyRetentionDeadlineUtc" IS NOT NULL
                           OR "PolicyEvaluatedAtUtc" IS NOT NULL
                           OR "PolicyStateBindingsJson" IS NOT NULL
                           OR "PolicyStateBindingsSha256" IS NOT NULL
                           OR "PolicyRequiresDistinctExecutor" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while scoped or version 3 processing-ledger entries exist.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."execution_batches"
                        WHERE "CaseKind" <> 1
                           OR "ScopeKind" <> 1
                           OR "PropertyId" IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while tenant-scoped execution batches exist.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."execution_work_items" AS w
                        LEFT JOIN "data-rights"."cases" AS c
                          ON c."ScopeId" = w."ScopeId"
                         AND c."Id" = w."CaseId"
                        WHERE c."Id" IS NULL
                           OR w."CaseKind" <> 1
                           OR w."ScopeKind" <> 1
                           OR w."PropertyId" IS NULL
                           OR w."PolicyEvidenceSchemaVersion" <> 1
                           OR w."PolicyPropertyVersion" IS DISTINCT FROM c."ApprovalEvidencePropertyVersion"
                           OR w."PolicyOperatingCountryCode" IS DISTINCT FROM c."ApprovalEvidenceOperatingCountryCode"
                           OR w."PolicyPurposeCode" IS DISTINCT FROM c."ApprovalEvidencePurposeCode"
                           OR w."PolicySurface" IS DISTINCT FROM c."ApprovalEvidenceSurface"
                           OR w."PolicySourceProvenance" IS DISTINCT FROM c."ApprovalEvidenceSourceProvenance"
                           OR w."PolicyRetentionDataClass" <> ''
                           OR w."PolicyRetentionTrigger" <> ''
                           OR w."PolicyRetentionTriggeredAtUtc" IS NOT NULL
                           OR w."PolicyRetentionDeadlineUtc" IS NOT NULL
                           OR w."PolicyEvaluatedAtUtc" IS DISTINCT FROM c."ApprovalEvidenceEvaluatedAtUtc"
                           OR w."PolicyStateBindingsJson" <> '[]'
                           OR w."PolicyStateBindingsSha256" <>
                              '4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945'
                           OR w."PolicyRequiresDistinctExecutor" IS DISTINCT FROM TRUE
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while non-legacy execution policy evidence exists.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "data-rights"."cases"
                        WHERE ("Kind" = 3 AND "RequestedOperations" = 16)
                           OR "ApprovalEvidenceSchemaVersion" = 2
                           OR (
                               "Decision" = 1
                               AND "RequestedOperations" = 16
                               AND (
                                   "ApprovalEvidenceCaseKind" <> 1
                                   OR "ApprovalEvidenceScopeKind" <> 1
                                   OR "ApprovalEvidenceRetentionDataClass" <> ''
                                   OR "ApprovalEvidenceRetentionTrigger" <> ''
                                   OR "ApprovalEvidenceRetentionTriggeredAtUtc" IS NOT NULL
                                   OR "ApprovalEvidenceRetentionDeadlineUtc" IS NOT NULL
                                   OR "ApprovalEvidenceStateBindingsJson" <> '[]'
                                   OR "ApprovalEvidenceStateBindingsSha256" <>
                                      '4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945'
                               )
                           )
                           OR (
                               ("Decision" <> 1 OR "RequestedOperations" <> 16)
                               AND (
                                   "ApprovalEvidenceCaseKind" IS NOT NULL
                                   OR "ApprovalEvidenceScopeKind" IS NOT NULL
                                   OR "ApprovalEvidenceRetentionDataClass" IS NOT NULL
                                   OR "ApprovalEvidenceRetentionTrigger" IS NOT NULL
                                   OR "ApprovalEvidenceRetentionTriggeredAtUtc" IS NOT NULL
                                   OR "ApprovalEvidenceRetentionDeadlineUtc" IS NOT NULL
                                   OR "ApprovalEvidenceStateBindingsJson" IS NOT NULL
                                   OR "ApprovalEvidenceStateBindingsSha256" IS NOT NULL
                               )
                           )
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade while scoped case approval evidence exists.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_contract",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_policy",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_result_version",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_scope",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_execution_work_items_ScopeId_CaseKind_ScopeKind_PropertyId_~",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_policy",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_work_items_scope",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_execution_batches_scope",
                schema: "data-rights",
                table: "execution_batches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_approval_policy_evidence",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "CaseKind",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyEvaluatedAtUtc",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyOperatingCountryCode",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyPropertyVersion",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyPurposeCode",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyRequiresDistinctExecutor",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionDataClass",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionDeadlineUtc",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionTrigger",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionTriggeredAtUtc",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicySourceProvenance",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyStateBindingsJson",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicyStateBindingsSha256",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "PolicySurface",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "ScopeKind",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "CaseKind",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyEvaluatedAtUtc",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyOperatingCountryCode",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyPropertyVersion",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyPurposeCode",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyRequiresDistinctExecutor",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionDataClass",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionDeadlineUtc",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionTrigger",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyRetentionTriggeredAtUtc",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicySourceProvenance",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyStateBindingsJson",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicyStateBindingsSha256",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "PolicySurface",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "ScopeKind",
                schema: "data-rights",
                table: "execution_work_items");

            migrationBuilder.DropColumn(
                name: "CaseKind",
                schema: "data-rights",
                table: "execution_batches");

            migrationBuilder.DropColumn(
                name: "ScopeKind",
                schema: "data-rights",
                table: "execution_batches");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceCaseKind",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceRetentionDataClass",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceRetentionDeadlineUtc",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceRetentionTrigger",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceRetentionTriggeredAtUtc",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceScopeKind",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceStateBindingsJson",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceStateBindingsSha256",
                schema: "data-rights",
                table: "cases");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoutingPropertyId",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                schema: "data-rights",
                table: "execution_work_items",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                schema: "data-rights",
                table: "execution_batches",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_contract",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "\"ContractVersion\" BETWEEN 1 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_policy",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "\"PolicyEvidenceSchemaVersion\" = 1 AND length(trim(\"PolicyId\")) > 0 AND \"PolicyVersion\" >= 1 AND length(trim(\"RetentionPolicyId\")) > 0 AND \"RetentionPolicyVersion\" >= 1 AND char_length(\"PolicyContentSha256\") = 64");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_result_version",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "(\"ContractVersion\" = 1 AND \"ResultingRecordVersion\" IS NULL) OR (\"ContractVersion\" = 2 AND \"ResultingRecordVersion\" >= 1)");

            migrationBuilder.CreateIndex(
                name: "IX_execution_work_items_ScopeId_PropertyId_State_CreatedAtUtc_~",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "PropertyId", "State", "CreatedAtUtc", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_execution_work_items_policy",
                schema: "data-rights",
                table: "execution_work_items",
                sql: "\"PolicyEvidenceSchemaVersion\" = 1 AND length(trim(\"PolicyId\")) > 0 AND \"PolicyVersion\" >= 1 AND length(trim(\"RetentionPolicyId\")) > 0 AND \"RetentionPolicyVersion\" >= 1 AND char_length(\"PolicyContentSha256\") = 64");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_approval_policy_evidence",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Decision\" = 1 AND \"RequestedOperations\" = 16 AND \"ApprovalEvidenceSchemaVersion\" = 1 AND \"ApprovalEvidencePropertyId\" = \"PropertyId\" AND \"ApprovalEvidencePropertyVersion\" > 0 AND \"ApprovalEvidenceOperatingCountryCode\" IS NOT NULL AND char_length(\"ApprovalEvidenceOperatingCountryCode\") = 2 AND \"ApprovalEvidencePolicyId\" IS NOT NULL AND \"ApprovalEvidencePolicyVersion\" > 0 AND \"ApprovalEvidenceRetentionPolicyId\" IS NOT NULL AND \"ApprovalEvidenceRetentionPolicyVersion\" > 0 AND \"ApprovalEvidenceContentSha256\" IS NOT NULL AND char_length(\"ApprovalEvidenceContentSha256\") = 64 AND \"ApprovalEvidencePurposeCode\" = 'data-rights-anonymisation' AND \"ApprovalEvidenceSurface\" = 'erasure' AND \"ApprovalEvidenceSourceProvenance\" = 'authorized-workspace-operator' AND \"ApprovalEvidenceEvaluatedAtUtc\" IS NOT NULL AND \"ApprovalEvidenceRequiresDistinctExecutor\" = TRUE) OR ((\"Decision\" <> 1 OR \"RequestedOperations\" <> 16) AND \"ApprovalEvidenceSchemaVersion\" IS NULL AND \"ApprovalEvidencePropertyId\" IS NULL AND \"ApprovalEvidencePropertyVersion\" IS NULL AND \"ApprovalEvidenceOperatingCountryCode\" IS NULL AND \"ApprovalEvidencePolicyId\" IS NULL AND \"ApprovalEvidencePolicyVersion\" IS NULL AND \"ApprovalEvidenceRetentionPolicyId\" IS NULL AND \"ApprovalEvidenceRetentionPolicyVersion\" IS NULL AND \"ApprovalEvidenceContentSha256\" IS NULL AND \"ApprovalEvidencePurposeCode\" IS NULL AND \"ApprovalEvidenceSurface\" IS NULL AND \"ApprovalEvidenceSourceProvenance\" IS NULL AND \"ApprovalEvidenceEvaluatedAtUtc\" IS NULL AND \"ApprovalEvidenceRequiresDistinctExecutor\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_cases_operations",
                schema: "data-rights",
                table: "cases",
                sql: "(\"Kind\" <> 3 AND \"RequestedOperations\" BETWEEN 1 AND 31) OR (\"Kind\" = 3 AND \"RequestedOperations\" IN (1, 2, 4))");
        }
    }
}
