using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingLedgerResultVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_contract",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.AddColumn<long>(
                name: "ResultingRecordVersion",
                schema: "data-rights",
                table: "processing_ledger_entries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_contract",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "\"ContractVersion\" BETWEEN 1 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_result_version",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "(\"ContractVersion\" = 1 AND \"ResultingRecordVersion\" IS NULL) OR (\"ContractVersion\" = 2 AND \"ResultingRecordVersion\" >= 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_contract",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_data_rights_processing_ledger_result_version",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropColumn(
                name: "ResultingRecordVersion",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_data_rights_processing_ledger_contract",
                schema: "data-rights",
                table: "processing_ledger_entries",
                sql: "\"ContractVersion\" = 1");
        }
    }
}
