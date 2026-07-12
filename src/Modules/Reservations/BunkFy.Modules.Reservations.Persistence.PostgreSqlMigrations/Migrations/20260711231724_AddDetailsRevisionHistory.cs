using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BunkFy.Modules.Reservations.Persistence.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDetailsRevisionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DetailsRevision",
                schema: "reservations",
                table: "reservations",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "LastDetailsActorId",
                schema: "reservations",
                table: "reservations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastDetailsAdapterConnectionId",
                schema: "reservations",
                table: "reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastDetailsChangeOrigin",
                schema: "reservations",
                table: "reservations",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDetailsChangedAtUtc",
                schema: "reservations",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastDetailsExternalOperationId",
                schema: "reservations",
                table: "reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reservation_details_history",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromRevision = table.Column<long>(type: "bigint", nullable: false),
                    ToRevision = table.Column<long>(type: "bigint", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AdapterConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationDeduplicationKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedFieldsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    BeforeSnapshotJson = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: true),
                    AfterSnapshotJson = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: false),
                    AfterSnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_details_history", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_details_history_ScopeId_OperationDeduplicationK~",
                schema: "reservations",
                table: "reservation_details_history",
                columns: new[] { "ScopeId", "OperationDeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_details_history_ScopeId_PropertyId_OccurredAtUtc",
                schema: "reservations",
                table: "reservation_details_history",
                columns: new[] { "ScopeId", "PropertyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_details_history_ScopeId_ReservationId_ToRevision",
                schema: "reservations",
                table: "reservation_details_history",
                columns: new[] { "ScopeId", "ReservationId", "ToRevision" },
                unique: true);

            migrationBuilder.Sql(
                """
                UPDATE reservations.reservations
                SET "LastDetailsChangedAtUtc" = "CreatedAtUtc";

                INSERT INTO reservations.reservation_details_history (
                    "Id",
                    "ScopeId",
                    "ReservationId",
                    "PropertyId",
                    "FromRevision",
                    "ToRevision",
                    "Origin",
                    "ActorId",
                    "AdapterConnectionId",
                    "ExternalOperationId",
                    "OperationDeduplicationKey",
                    "CorrelationId",
                    "ChangedFieldsJson",
                    "BeforeSnapshotJson",
                    "AfterSnapshotJson",
                    "AfterSnapshotHash",
                    "OccurredAtUtc")
                SELECT
                    r."Id",
                    r."ScopeId",
                    r."Id",
                    r."PropertyId",
                    0,
                    1,
                    4,
                    NULL,
                    NULL,
                    NULL,
                    'backfill:' || replace(r."Id"::text, '-', ''),
                    r."Id",
                    '["Arrival","Departure","RequestedUnits","PrimaryGuestName","Email","Phone","GuestCount","Notes"]',
                    NULL,
                    jsonb_build_object(
                        'arrival', r."Arrival",
                        'departure', r."Departure",
                        'inventoryUnitIds', COALESCE((
                            SELECT jsonb_agg(u."Id" ORDER BY u."Id")
                            FROM reservations.requested_inventory_units AS u
                            WHERE u."ScopeId" = r."ScopeId" AND u."ReservationId" = r."Id"
                        ), '[]'::jsonb),
                        'primaryGuestName', r."PrimaryGuestName",
                        'email', r."Email",
                        'phone', r."Phone",
                        'guestCount', r."GuestCount",
                        'notes', r."Notes")::text,
                    repeat('0', 64),
                    r."CreatedAtUtc"
                FROM reservations.reservations AS r;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastDetailsChangedAtUtc",
                schema: "reservations",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_details_history",
                schema: "reservations");

            migrationBuilder.DropColumn(
                name: "DetailsRevision",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "LastDetailsActorId",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "LastDetailsAdapterConnectionId",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "LastDetailsChangeOrigin",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "LastDetailsChangedAtUtc",
                schema: "reservations",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "LastDetailsExternalOperationId",
                schema: "reservations",
                table: "reservations");
        }
    }
}
