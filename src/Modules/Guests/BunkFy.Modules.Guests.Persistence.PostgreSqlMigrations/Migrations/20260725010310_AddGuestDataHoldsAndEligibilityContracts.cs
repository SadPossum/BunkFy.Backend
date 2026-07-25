using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Guests.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestDataHoldsAndEligibilityContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectionContractVersion",
                schema: "guests",
                table: "stay_history",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "data_holds",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    PlacedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlacedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_holds", x => x.Id);
                    table.UniqueConstraint("AK_data_holds_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_guest_data_holds_lifecycle", "(\"State\" = 1 AND \"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR (\"State\" = 2 AND \"ReleasedBy\" IS NOT NULL AND \"ReleasedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" >= \"PlacedAtUtc\" AND \"Version\" >= 2)");
                    table.CheckConstraint("CK_guest_data_holds_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "data_hold_receipts",
                schema: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SelectedGuestVersion = table.Column<long>(type: "bigint", nullable: false),
                    ResultingHoldVersion = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_hold_receipts", x => x.Id);
                    table.CheckConstraint("CK_guest_data_hold_receipts_versions", "\"SelectedGuestVersion\" >= 1 AND ((\"Action\" = 1 AND \"ResultingHoldVersion\" = 1) OR (\"Action\" = 2 AND \"ResultingHoldVersion\" >= 2))");
                    table.ForeignKey(
                        name: "FK_data_hold_receipts_data_holds_ScopeId_HoldId",
                        columns: x => new { x.ScopeId, x.HoldId },
                        principalSchema: "guests",
                        principalTable: "data_holds",
                        principalColumns: new[] { "ScopeId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_guests_stay_history_contract_version",
                schema: "guests",
                table: "stay_history",
                sql: "\"ProjectionContractVersion\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_data_hold_receipts_ScopeId_HoldId_Action",
                schema: "guests",
                table: "data_hold_receipts",
                columns: new[] { "ScopeId", "HoldId", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_hold_receipts_ScopeId_IdempotencyKey",
                schema: "guests",
                table: "data_hold_receipts",
                columns: new[] { "ScopeId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_hold_receipts_ScopeId_PropertyId_GuestId_HoldId_Comple~",
                schema: "guests",
                table: "data_hold_receipts",
                columns: new[] { "ScopeId", "PropertyId", "GuestId", "HoldId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_data_holds_ScopeId_GuestId_State_PropertyId_Id",
                schema: "guests",
                table: "data_holds",
                columns: new[] { "ScopeId", "GuestId", "State", "PropertyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_data_holds_ScopeId_PropertyId_GuestId_PlacedAtUtc_Id",
                schema: "guests",
                table: "data_holds",
                columns: new[] { "ScopeId", "PropertyId", "GuestId", "PlacedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_data_holds_ScopeId_PropertyId_GuestId_State_PlacedAtUtc_Id",
                schema: "guests",
                table: "data_holds",
                columns: new[] { "ScopeId", "PropertyId", "GuestId", "State", "PlacedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_hold_receipts",
                schema: "guests");

            migrationBuilder.DropTable(
                name: "data_holds",
                schema: "guests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_guests_stay_history_contract_version",
                schema: "guests",
                table: "stay_history");

            migrationBuilder.DropColumn(
                name: "ProjectionContractVersion",
                schema: "guests",
                table: "stay_history");
        }
    }
}
