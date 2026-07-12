using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Ingestion.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RetentionFenceVersion",
                schema: "ingestion",
                table: "property_projection",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "legal_holds",
                schema: "ingestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    PlacedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlacedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_holds", x => x.Id);
                    table.UniqueConstraint("AK_legal_holds_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_legal_holds_lifecycle", "(\"State\" = 1 AND \"ReleasedBy\" IS NULL AND \"ReleaseReason\" IS NULL AND \"ReleasedAtUtc\" IS NULL) OR (\"State\" = 2 AND \"ReleasedBy\" IS NOT NULL AND length(trim(\"ReleasedBy\")) > 0 AND \"ReleaseReason\" IS NOT NULL AND length(trim(\"ReleaseReason\")) > 0 AND \"ReleasedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" >= \"PlacedAtUtc\")");
                    table.CheckConstraint("CK_legal_holds_placed_by", "length(trim(\"PlacedBy\")) > 0");
                    table.CheckConstraint("CK_legal_holds_reason", "length(trim(\"Reason\")) > 0");
                    table.CheckConstraint("CK_legal_holds_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_legal_holds_property_projection_ScopeId_PropertyId",
                        columns: x => new { x.ScopeId, x.PropertyId },
                        principalSchema: "ingestion",
                        principalTable: "property_projection",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_property_projection_retention_fence",
                schema: "ingestion",
                table: "property_projection",
                sql: "\"RetentionFenceVersion\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_legal_holds_ScopeId_PropertyId_PlacedAtUtc_Id",
                schema: "ingestion",
                table: "legal_holds",
                columns: new[] { "ScopeId", "PropertyId", "PlacedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_holds_ScopeId_PropertyId_State",
                schema: "ingestion",
                table: "legal_holds",
                columns: new[] { "ScopeId", "PropertyId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legal_holds",
                schema: "ingestion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_property_projection_retention_fence",
                schema: "ingestion",
                table: "property_projection");

            migrationBuilder.DropColumn(
                name: "RetentionFenceVersion",
                schema: "ingestion",
                table: "property_projection");
        }
    }
}
