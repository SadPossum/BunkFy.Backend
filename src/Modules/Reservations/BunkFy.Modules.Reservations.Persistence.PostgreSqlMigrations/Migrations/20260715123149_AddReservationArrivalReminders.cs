using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationArrivalReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arrival_reminders",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DetailsRevision = table.Column<long>(type: "bigint", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedArrivalTime = table.Column<TimeOnly>(type: "time(0) without time zone", nullable: false),
                    ExpectedArrivalAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeadTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arrival_reminders", x => x.Id);
                    table.UniqueConstraint("AK_arrival_reminders_ScopeId_Id", x => new { x.ScopeId, x.Id });
                    table.CheckConstraint("CK_arrival_reminders_details_revision", "\"DetailsRevision\" >= 1");
                    table.CheckConstraint("CK_arrival_reminders_dispatch_state", "(\"State\" = 2 AND \"DispatchedAtUtc\" IS NOT NULL) OR (\"State\" <> 2 AND \"DispatchedAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_arrival_reminders_lead_time", "\"LeadTimeMinutes\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "property_projection",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsKnown = table.Column<bool>(type: "boolean", nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_projection", x => x.Id);
                    table.UniqueConstraint("AK_property_projection_ScopeId_Id", x => new { x.ScopeId, x.Id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_arrival_reminders_ScopeId_PropertyId_State",
                schema: "reservations",
                table: "arrival_reminders",
                columns: new[] { "ScopeId", "PropertyId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_arrival_reminders_ScopeId_ReservationId_DetailsRevision_Tim~",
                schema: "reservations",
                table: "arrival_reminders",
                columns: new[] { "ScopeId", "ReservationId", "DetailsRevision", "TimeZoneId", "LeadTimeMinutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arrival_reminders_ScopeId_State_DueAtUtc",
                schema: "reservations",
                table: "arrival_reminders",
                columns: new[] { "ScopeId", "State", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_property_projection_ScopeId_IsActive",
                schema: "reservations",
                table: "property_projection",
                columns: new[] { "ScopeId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arrival_reminders",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "property_projection",
                schema: "reservations");

        }
    }
}
