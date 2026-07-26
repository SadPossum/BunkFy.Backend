using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionDataRightsReceiptLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_observation_receipts_ScopeId_ConnectionId_ExternalId_Receiv~",
                schema: "ingestion",
                table: "observation_receipts",
                columns: new[] { "ScopeId", "ConnectionId", "ExternalId", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_observation_receipts_ScopeId_ConnectionId_ExternalId_Receiv~",
                schema: "ingestion",
                table: "observation_receipts");
        }
    }
}
