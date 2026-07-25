using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleDataRightsLedgerRestoreProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_processing_ledger_entries_execution_work_items_ScopeId_Work~",
                schema: "data-rights",
                table: "processing_ledger_entries");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_execution_work_items_ScopeId_Id",
                schema: "data-rights",
                table: "execution_work_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_execution_work_items_ScopeId_Id",
                schema: "data-rights",
                table: "execution_work_items",
                columns: new[] { "ScopeId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_processing_ledger_entries_execution_work_items_ScopeId_Work~",
                schema: "data-rights",
                table: "processing_ledger_entries",
                columns: new[] { "ScopeId", "WorkItemId" },
                principalSchema: "data-rights",
                principalTable: "execution_work_items",
                principalColumns: new[] { "ScopeId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
