using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestDataRightsCorrectionReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guest_data_rights_correction_receipts",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    CurrentRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    ChangedFieldsMask = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_data_rights_correction_receipts", x => x.Id);
                    table.UniqueConstraint("AK_guest_data_rights_correction_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_data_rights_correction_receipts_approval_revision", "\"ApprovalRevision\" >= 1");
                    table.CheckConstraint("CK_guest_data_rights_correction_receipts_changed_fields", "\"ChangedFieldsMask\" BETWEEN 1 AND 255");
                    table.CheckConstraint("CK_guest_data_rights_correction_receipts_versions", "\"SelectedRecordVersion\" >= 1 AND \"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_guest_data_rights_correction_receipts_ScopeId_GuestId_Curre~",
                schema: "guests",
                table: "guest_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "GuestId", "CurrentRecordVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_guest_data_rights_correction_receipts_ScopeId_IdempotencyKey",
                schema: "guests",
                table: "guest_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guest_data_rights_correction_receipts_ScopeId_PropertyId_Ca~",
                schema: "guests",
                table: "guest_data_rights_correction_receipts",
                columns: new[] { "ScopeId", "PropertyId", "CaseId", "ApprovalRevision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guest_data_rights_correction_receipts",
                schema: "guests");
        }
    }
}
