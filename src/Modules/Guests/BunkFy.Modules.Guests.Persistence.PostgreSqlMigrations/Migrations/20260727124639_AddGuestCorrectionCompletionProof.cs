using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestCorrectionCompletionProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompletionEventId",
                schema: "guests",
                table: "guest_data_rights_correction_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractVersion",
                schema: "guests",
                table: "guest_data_rights_correction_receipts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE guests.guest_data_rights_correction_receipts
                SET "CompletionEventId" =
                    md5("Id"::text || ':guest-correction-completion')::uuid
                WHERE "CompletionEventId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompletionEventId",
                schema: "guests",
                table: "guest_data_rights_correction_receipts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_data_rights_correction_receipts_ScopeId_CompletionEve~",
                schema: "guests",
                table: "guest_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "CompletionEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_data_rights_correction_receipts_ScopeId_EventId",
                schema: "guests",
                table: "guest_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "EventId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_guest_data_rights_correction_receipts_contract",
                schema: "guests",
                table: "guest_data_rights_correction_receipts",
                sql: "\"ContractVersion\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_guest_data_rights_correction_receipts_ScopeId_CompletionEve~",
                schema: "guests",
                table: "guest_data_rights_correction_receipts");

            migrationBuilder.DropIndex(
                name: "IX_guest_data_rights_correction_receipts_ScopeId_EventId",
                schema: "guests",
                table: "guest_data_rights_correction_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_guest_data_rights_correction_receipts_contract",
                schema: "guests",
                table: "guest_data_rights_correction_receipts");

            migrationBuilder.DropColumn(
                name: "CompletionEventId",
                schema: "guests",
                table: "guest_data_rights_correction_receipts");

            migrationBuilder.DropColumn(
                name: "ContractVersion",
                schema: "guests",
                table: "guest_data_rights_correction_receipts");
        }
    }
}
