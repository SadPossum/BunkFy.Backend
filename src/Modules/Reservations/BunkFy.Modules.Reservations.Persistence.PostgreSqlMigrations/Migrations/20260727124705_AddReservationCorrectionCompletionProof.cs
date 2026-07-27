using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationCorrectionCompletionProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompletionEventId",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE reservations.reservation_data_rights_correction_receipts
                SET "CompletionEventId" =
                    md5("Id"::text || ':reservation-correction-completion')::uuid
                WHERE "CompletionEventId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompletionEventId",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_data_rights_correction_receipts_ScopeId_Complet~",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "CompletionEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reservation_data_rights_correction_receipts_ScopeId_Complet~",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts");

            migrationBuilder.DropColumn(
                name: "CompletionEventId",
                schema: "reservations",
                table: "reservation_data_rights_correction_receipts");
        }
    }
}
