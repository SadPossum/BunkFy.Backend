using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Staff.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffDataRightsCorrectionReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_rights_correction_receipts",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRevision = table.Column<long>(type: "bigint", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    CurrentRecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    ChangedFieldsMask = table.Column<int>(type: "integer", nullable: false),
                    RequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ProfileEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletionEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_rights_correction_receipts", x => x.Id);
                    table.UniqueConstraint("AK_data_rights_correction_receipts_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_staff_data_rights_correction_receipts_approval", "\"ApprovalRevision\" >= 1");
                    table.CheckConstraint("CK_staff_data_rights_correction_receipts_contract", "\"ContractVersion\" = 1");
                    table.CheckConstraint("CK_staff_data_rights_correction_receipts_digest", "char_length(\"RequestSha256\") = 64");
                    table.CheckConstraint("CK_staff_data_rights_correction_receipts_fields", "\"ChangedFieldsMask\" BETWEEN 1 AND 127");
                    table.CheckConstraint("CK_staff_data_rights_correction_receipts_versions", "\"SelectedRecordVersion\" >= 1 AND \"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_data_rights_correction_receipts_ScopeId_CaseId_ApprovalRevi~",
                schema: "staff",
                table: "data_rights_correction_receipts",
                columns: new[] { "ScopeId", "CaseId", "ApprovalRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_data_rights_correction_receipts_ScopeId_CompletionEventId",
                schema: "staff",
                table: "data_rights_correction_receipts",
                columns: new[] { "ScopeId", "CompletionEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_rights_correction_receipts_ScopeId_ExecutionId",
                schema: "staff",
                table: "data_rights_correction_receipts",
                columns: new[] { "ScopeId", "ExecutionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_rights_correction_receipts_ScopeId_ProfileEventId",
                schema: "staff",
                table: "data_rights_correction_receipts",
                columns: new[] { "ScopeId", "ProfileEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_rights_correction_receipts_ScopeId_StaffMemberId_Curre~",
                schema: "staff",
                table: "data_rights_correction_receipts",
                columns: new[] { "ScopeId", "StaffMemberId", "CurrentRecordVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_rights_correction_receipts",
                schema: "staff");
        }
    }
}
