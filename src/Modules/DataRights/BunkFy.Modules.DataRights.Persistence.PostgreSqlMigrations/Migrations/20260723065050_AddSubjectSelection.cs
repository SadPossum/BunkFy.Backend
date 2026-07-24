using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.DataRights.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "selected_subjects",
                schema: "data-rights",
                columns: table => new
                {
                    OwnerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    SelectedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SelectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_selected_subjects", x => new { x.CaseId, x.OwnerKey, x.RecordType, x.RecordId });
                    table.CheckConstraint("CK_data_rights_selected_subjects_owner", "length(trim(\"OwnerKey\")) > 0");
                    table.CheckConstraint("CK_data_rights_selected_subjects_record_type", "length(trim(\"RecordType\")) > 0");
                    table.CheckConstraint("CK_data_rights_selected_subjects_record_version", "\"RecordVersion\" >= 1");
                    table.CheckConstraint("CK_data_rights_selected_subjects_selected_by", "length(trim(\"SelectedBy\")) > 0");
                    table.ForeignKey(
                        name: "FK_selected_subjects_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "data-rights",
                        principalTable: "cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "selected_subjects",
                schema: "data-rights");
        }
    }
}
