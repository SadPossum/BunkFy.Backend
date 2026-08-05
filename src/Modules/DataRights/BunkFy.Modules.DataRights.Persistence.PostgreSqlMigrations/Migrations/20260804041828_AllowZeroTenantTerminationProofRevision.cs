using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AllowZeroTenantTerminationProofRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_owner_result",
                schema: "data-rights",
                table: "tenant_termination_owner_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_export_fragment_proof",
                schema: "data-rights",
                table: "tenant_termination_export_fragments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_owner_result",
                schema: "data-rights",
                table: "tenant_termination_owner_work_items",
                sql: "(\"State\" IN (1, 2) AND \"ResultCode\" IS NULL AND \"AffectedCount\" IS NULL AND \"RetainedMinimumCount\" IS NULL AND \"RemainingActiveCount\" IS NULL AND \"HoldReviewAtUtc\" IS NULL AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultRecordedAtUtc\" IS NULL) OR (\"State\" = 6 AND \"ResultCode\" IS NOT NULL AND \"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND \"RemainingActiveCount\" = 0 AND \"HoldReviewAtUtc\" IS NULL AND \"SelectedProofRevision\" >= 0 AND \"ResultingProofRevision\" >= \"SelectedProofRevision\" AND \"ResultRecordedAtUtc\" IS NOT NULL) OR (\"State\" = 4 AND \"ResultCode\" IS NOT NULL AND \"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND \"RemainingActiveCount\" >= 0 AND \"HoldReviewAtUtc\" >= \"ResultRecordedAtUtc\" AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultRecordedAtUtc\" IS NOT NULL) OR (\"State\" IN (3, 5) AND \"ResultCode\" IS NOT NULL AND \"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND \"RemainingActiveCount\" >= 0 AND \"HoldReviewAtUtc\" IS NULL AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultRecordedAtUtc\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_export_fragment_proof",
                schema: "data-rights",
                table: "tenant_termination_export_fragments",
                sql: "(\"RecordCount\" IS NULL AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultCode\" IS NULL) OR (\"RecordCount\" BETWEEN 0 AND 1000000 AND \"SelectedProofRevision\" >= 0 AND \"ResultingProofRevision\" = \"SelectedProofRevision\" AND \"ResultCode\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_owner_result",
                schema: "data-rights",
                table: "tenant_termination_owner_work_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_tenant_termination_export_fragment_proof",
                schema: "data-rights",
                table: "tenant_termination_export_fragments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_owner_result",
                schema: "data-rights",
                table: "tenant_termination_owner_work_items",
                sql: "(\"State\" IN (1, 2) AND \"ResultCode\" IS NULL AND \"AffectedCount\" IS NULL AND \"RetainedMinimumCount\" IS NULL AND \"RemainingActiveCount\" IS NULL AND \"HoldReviewAtUtc\" IS NULL AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultRecordedAtUtc\" IS NULL) OR (\"State\" = 6 AND \"ResultCode\" IS NOT NULL AND \"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND \"RemainingActiveCount\" = 0 AND \"HoldReviewAtUtc\" IS NULL AND \"SelectedProofRevision\" >= 1 AND \"ResultingProofRevision\" >= \"SelectedProofRevision\" AND \"ResultRecordedAtUtc\" IS NOT NULL) OR (\"State\" = 4 AND \"ResultCode\" IS NOT NULL AND \"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND \"RemainingActiveCount\" >= 0 AND \"HoldReviewAtUtc\" >= \"ResultRecordedAtUtc\" AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultRecordedAtUtc\" IS NOT NULL) OR (\"State\" IN (3, 5) AND \"ResultCode\" IS NOT NULL AND \"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND \"RemainingActiveCount\" >= 0 AND \"HoldReviewAtUtc\" IS NULL AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultRecordedAtUtc\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_tenant_termination_export_fragment_proof",
                schema: "data-rights",
                table: "tenant_termination_export_fragments",
                sql: "(\"RecordCount\" IS NULL AND \"SelectedProofRevision\" IS NULL AND \"ResultingProofRevision\" IS NULL AND \"ResultCode\" IS NULL) OR (\"RecordCount\" BETWEEN 0 AND 1000000 AND \"SelectedProofRevision\" >= 1 AND \"ResultingProofRevision\" = \"SelectedProofRevision\" AND \"ResultCode\" IS NOT NULL)");
        }
    }
}
